using System.Text.Json;
using OnlineSchoolDiary.Server.Services;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Protocol;
using OnlineSchoolDiary.Shared.Security;

namespace OnlineSchoolDiary.Server.Rpc;

public sealed class RpcMethods
{
    private readonly DataService _data;
    private readonly SessionManager _sessions;

    public RpcMethods(DataService data, SessionManager sessions)
    {
        _data = data;
        _sessions = sessions;
    }

    private static T RequirePayload<T>(RpcRequest req)
    {
        if (req.Payload is null) throw new InvalidOperationException("Missing payload.");
        return req.Payload.Value.Deserialize<T>(JsonDefaults.Options)
               ?? throw new InvalidOperationException("Invalid payload.");
    }

    private User RequireAuth(string? token)
    {
        var user = _sessions.GetUser(token);
        return user ?? throw new InvalidOperationException("Not authenticated.");
    }

    public async Task<RpcResponse> HandleAsync(RpcRequest req, string? sessionToken)
    {
        try
        {
            return req.Method switch
            {
                "auth.login" => Login(req),
                "meta.bootstrap" => Bootstrap(sessionToken, req),
                "meta.contacts" => Contacts(req, sessionToken),

                "admin.users.list" => ListUsers(req, sessionToken),
                "admin.users.create" => await CreateUserAsync(req, sessionToken),
                "admin.users.update" => await UpdateUserAsync(req, sessionToken),
                "admin.users.delete" => await DeleteUserAsync(req, sessionToken),

                "admin.assignments.list" => ListAssignments(req, sessionToken),
                "admin.assignments.add" => await AddAssignmentAsync(req, sessionToken),
                "admin.assignments.delete" => await DeleteAssignmentAsync(req, sessionToken),

                "admin.notifications.send" => await SendNotificationAsync(req, sessionToken),
                "notifications.list" => ListNotifications(req, sessionToken),
                "admin.reports" => Reports(req, sessionToken),

                "teacher.diary.list" => ListDiaryForTeacher(req, sessionToken),
                "teacher.diary.upsert" => await UpsertDiaryAsync(req, sessionToken),
                "teacher.diary.delete" => await DeleteDiaryAsync(req, sessionToken),
                "teacher.diary.ackCounts" => AckCountsForTeacher(req, sessionToken),

                "student.diary.list" => ListDiaryForStudent(req, sessionToken),
                "student.diary.ack" => await AckDiaryAsync(req, sessionToken),

                "parent.childDiary.list" => ListChildDiary(req, sessionToken),

                "chat.send" => await SendChatAsync(req, sessionToken),
                "chat.get" => GetChat(req, sessionToken),

                _ => new RpcResponse(req.RequestId, false, $"Unknown method: {req.Method}", null)
            };
        }
        catch (Exception ex)
        {
            return new RpcResponse(req.RequestId, false, ex.Message, null);
        }
    }

    private RpcResponse Login(RpcRequest req)
    {
        var payload = RequirePayload<LoginRequest>(req);
        var user = _data.State.Users.FirstOrDefault(u => u.Username.Equals(payload.Username, StringComparison.OrdinalIgnoreCase));
        if (user is null || !PasswordHasher.Verify(payload.Password, user.PasswordHash))
            return new RpcResponse(req.RequestId, false, "Invalid username or password.", null);

        var token = _sessions.CreateSession(user);
        var response = new { token, user };
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(response));
    }

    private RpcResponse Bootstrap(string? token, RpcRequest req)
    {
        var user = RequireAuth(token);
        var state = _data.State;
        var payload = new
        {
            user,
            classes = state.Classes,
            subjects = state.Subjects,
            assignments = state.Assignments
        };
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(payload));
    }

    private RpcResponse Contacts(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        var teachers = _data.State.Users.Where(u => u.Role == UserRole.Teacher).ToList();
        var parents = _data.State.Users.Where(u => u.Role == UserRole.Parent).ToList();
        var students = _data.State.Users.Where(u => u.Role == UserRole.Student).ToList();

        object payload = user.Role switch
        {
            UserRole.Teacher => new
            {
                teachers = new[] { user },
                parents,
                students
            },
            UserRole.Parent => new
            {
                teachers,
                parents = new[] { user },
                students
            },
            _ => new { teachers, parents, students }
        };

        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(payload));
    }

    private static void RequireRole(User user, UserRole role)
    {
        if (user.Role != role) throw new InvalidOperationException("Access denied.");
    }

    private RpcResponse ListUsers(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(_data.State.Users));
    }

    private async Task<RpcResponse> CreateUserAsync(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);

        var p = RequirePayload<CreateUserRequest>(req);
        if (_data.State.Users.Any(u => u.Username.Equals(p.Username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Username already exists.");

        var created = new User(
            Guid.NewGuid(),
            p.Username.Trim(),
            PasswordHasher.Hash(p.Password),
            p.Role,
            p.FullName.Trim(),
            p.ClassId,
            p.ChildStudentId
        );

        _data.State.Users.Add(created);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(created));
    }

    private async Task<RpcResponse> UpdateUserAsync(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);

        var p = RequirePayload<UpdateUserRequest>(req);
        var idx = _data.State.Users.FindIndex(u => u.Id == p.Id);
        if (idx < 0) throw new InvalidOperationException("User not found.");

        var existing = _data.State.Users[idx];
        var updated = existing with
        {
            Username = p.Username.Trim(),
            Role = p.Role,
            FullName = p.FullName.Trim(),
            ClassId = p.ClassId,
            ChildStudentId = p.ChildStudentId
        };

        _data.State.Users[idx] = updated;
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(updated));
    }

    private async Task<RpcResponse> DeleteUserAsync(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);

        var id = RequirePayload<Guid>(req);
        _data.State.Users.RemoveAll(u => u.Id == id);
        _data.State.Assignments.RemoveAll(a => a.TeacherId == id);
        _data.State.DiaryEntries.RemoveAll(d => d.TeacherId == id);
        _data.State.Acks.RemoveAll(a => a.StudentId == id);
        _data.State.ChatMessages.RemoveAll(m => m.ParentId == id || m.TeacherId == id);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, null);
    }

    private RpcResponse ListAssignments(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(_data.State.Assignments));
    }

    private async Task<RpcResponse> AddAssignmentAsync(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);
        var p = RequirePayload<AssignTeacherRequest>(req);

        if (_data.State.Assignments.Any(a => a.TeacherId == p.TeacherId && a.ClassId == p.ClassId && a.SubjectId == p.SubjectId))
            throw new InvalidOperationException("Assignment already exists.");

        var assignment = new TeacherAssignment(Guid.NewGuid(), p.TeacherId, p.ClassId, p.SubjectId);
        _data.State.Assignments.Add(assignment);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(assignment));
    }

    private async Task<RpcResponse> DeleteAssignmentAsync(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);

        var id = RequirePayload<Guid>(req);
        _data.State.Assignments.RemoveAll(a => a.Id == id);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, null);
    }

    private async Task<RpcResponse> SendNotificationAsync(RpcRequest req, string? token)
    {
        var sender = RequireAuth(token);
        RequireRole(sender, UserRole.Admin);

        var p = RequirePayload<SendNotificationRequest>(req);
        var n = new Notification(
            Guid.NewGuid(),
            sender.Id,
            p.Title.Trim(),
            p.Message.Trim(),
            p.Audience,
            p.TargetRole,
            p.TargetClassId,
            p.TargetUserId,
            DateTimeOffset.UtcNow
        );
        _data.State.Notifications.Add(n);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(n));
    }

    private RpcResponse ListNotifications(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        var list = _data.State.Notifications
            .Where(n =>
                n.Audience == NotificationAudience.All ||
                (n.Audience == NotificationAudience.Role && n.TargetRole == user.Role) ||
                (n.Audience == NotificationAudience.Class && user.ClassId is not null && n.TargetClassId == user.ClassId) ||
                (n.Audience == NotificationAudience.User && n.TargetUserId == user.Id))
            .OrderByDescending(n => n.SentAt)
            .ToList();

        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(list));
    }

    private RpcResponse Reports(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        RequireRole(user, UserRole.Admin);
        var s = _data.State;
        var resp = new ReportsResponse(
            s.Users.Count,
            s.Users.Count(u => u.Role == UserRole.Teacher),
            s.Users.Count(u => u.Role == UserRole.Student),
            s.Users.Count(u => u.Role == UserRole.Parent),
            s.DiaryEntries.Count,
            s.Acks.Count
        );
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(resp));
    }

    private RpcResponse ListDiaryForTeacher(RpcRequest req, string? token)
    {
        var teacher = RequireAuth(token);
        RequireRole(teacher, UserRole.Teacher);

        var allowed = _data.State.Assignments.Where(a => a.TeacherId == teacher.Id).ToList();
        var allowedPairs = new HashSet<(string c, string s)>(allowed.Select(a => (a.ClassId, a.SubjectId)));

        var list = _data.State.DiaryEntries
            .Where(d => d.TeacherId == teacher.Id || allowedPairs.Contains((d.ClassId, d.SubjectId)))
            .OrderByDescending(d => d.Date)
            .ToList();

        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(list));
    }

    private async Task<RpcResponse> UpsertDiaryAsync(RpcRequest req, string? token)
    {
        var teacher = RequireAuth(token);
        RequireRole(teacher, UserRole.Teacher);
        var p = RequirePayload<UpsertDiaryRequest>(req);

        if (!_data.State.Assignments.Any(a => a.TeacherId == teacher.Id && a.ClassId == p.ClassId && a.SubjectId == p.SubjectId))
            throw new InvalidOperationException("You are not assigned to that class/subject.");

        DiaryEntry entry;
        if (p.Id is null || p.Id == Guid.Empty)
        {
            entry = new DiaryEntry(
                Guid.NewGuid(),
                p.ClassId,
                p.SubjectId,
                teacher.Id,
                p.Date,
                p.Title.Trim(),
                p.Text.Trim(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );
            _data.State.DiaryEntries.Add(entry);
        }
        else
        {
            var idx = _data.State.DiaryEntries.FindIndex(d => d.Id == p.Id.Value);
            if (idx < 0) throw new InvalidOperationException("Diary not found.");
            var existing = _data.State.DiaryEntries[idx];
            if (existing.TeacherId != teacher.Id) throw new InvalidOperationException("Access denied.");

            entry = existing with
            {
                Title = p.Title.Trim(),
                Text = p.Text.Trim(),
                Date = p.Date,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _data.State.DiaryEntries[idx] = entry;
        }

        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(entry));
    }

    private async Task<RpcResponse> DeleteDiaryAsync(RpcRequest req, string? token)
    {
        var teacher = RequireAuth(token);
        RequireRole(teacher, UserRole.Teacher);
        var id = RequirePayload<Guid>(req);

        var entry = _data.State.DiaryEntries.FirstOrDefault(d => d.Id == id);
        if (entry is null) return new RpcResponse(req.RequestId, true, null, null);
        if (entry.TeacherId != teacher.Id) throw new InvalidOperationException("Access denied.");

        _data.State.DiaryEntries.RemoveAll(d => d.Id == id);
        _data.State.Acks.RemoveAll(a => a.DiaryEntryId == id);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, null);
    }

    private RpcResponse AckCountsForTeacher(RpcRequest req, string? token)
    {
        var teacher = RequireAuth(token);
        RequireRole(teacher, UserRole.Teacher);

        var studentsByClass = _data.State.Users
            .Where(u => u.Role == UserRole.Student && u.ClassId is not null)
            .GroupBy(u => u.ClassId!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

        var entries = _data.State.DiaryEntries.Where(d => d.TeacherId == teacher.Id).ToList();
        var result = entries.Select(e =>
        {
            var totalStudents = studentsByClass.TryGetValue(e.ClassId, out var set) ? set.Count : 0;
            var ackCount = _data.State.Acks.Count(a => a.DiaryEntryId == e.Id);
            return new { diaryEntryId = e.Id, e.ClassId, e.SubjectId, e.Date, totalStudents, ackCount };
        }).ToList();

        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(result));
    }

    private RpcResponse ListDiaryForStudent(RpcRequest req, string? token)
    {
        var student = RequireAuth(token);
        RequireRole(student, UserRole.Student);
        if (string.IsNullOrWhiteSpace(student.ClassId)) throw new InvalidOperationException("Student has no class assigned.");

        var list = _data.State.DiaryEntries
            .Where(d => d.ClassId == student.ClassId)
            .OrderByDescending(d => d.Date)
            .ToList();

        var acked = _data.State.Acks
            .Where(a => a.StudentId == student.Id)
            .Select(a => a.DiaryEntryId)
            .ToHashSet();

        var payload = new { entries = list, ackedDiaryEntryIds = acked.ToList() };
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(payload));
    }

    private async Task<RpcResponse> AckDiaryAsync(RpcRequest req, string? token)
    {
        var student = RequireAuth(token);
        RequireRole(student, UserRole.Student);
        var p = RequirePayload<AckDiaryRequest>(req);

        if (_data.State.Acks.Any(a => a.StudentId == student.Id && a.DiaryEntryId == p.DiaryEntryId))
            return new RpcResponse(req.RequestId, true, null, null);

        _data.State.Acks.Add(new DiaryAcknowledgement(Guid.NewGuid(), p.DiaryEntryId, student.Id, DateTimeOffset.UtcNow));
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, null);
    }

    private RpcResponse ListChildDiary(RpcRequest req, string? token)
    {
        var parent = RequireAuth(token);
        RequireRole(parent, UserRole.Parent);
        if (parent.ChildStudentId is null) throw new InvalidOperationException("Parent has no linked child.");

        var child = _data.State.Users.FirstOrDefault(u => u.Id == parent.ChildStudentId && u.Role == UserRole.Student);
        if (child?.ClassId is null) throw new InvalidOperationException("Child not found or has no class.");

        var list = _data.State.DiaryEntries.Where(d => d.ClassId == child.ClassId).OrderByDescending(d => d.Date).ToList();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(list));
    }

    private async Task<RpcResponse> SendChatAsync(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        var p = RequirePayload<SendChatMessageRequest>(req);

        if (user.Role == UserRole.Teacher && user.Id != p.TeacherId) throw new InvalidOperationException("Access denied.");
        if (user.Role == UserRole.Parent && user.Id != p.ParentId) throw new InvalidOperationException("Access denied.");
        if (user.Role is not (UserRole.Teacher or UserRole.Parent)) throw new InvalidOperationException("Access denied.");

        var msg = new ChatMessage(Guid.NewGuid(), p.TeacherId, p.ParentId, user.Id, p.Text.Trim(), DateTimeOffset.UtcNow);
        _data.State.ChatMessages.Add(msg);
        await _data.PersistAllAsync();
        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(msg));
    }

    private RpcResponse GetChat(RpcRequest req, string? token)
    {
        var user = RequireAuth(token);
        var p = RequirePayload<GetChatRequest>(req);

        if (user.Role == UserRole.Teacher && user.Id != p.TeacherId) throw new InvalidOperationException("Access denied.");
        if (user.Role == UserRole.Parent && user.Id != p.ParentId) throw new InvalidOperationException("Access denied.");
        if (user.Role is not (UserRole.Teacher or UserRole.Parent)) throw new InvalidOperationException("Access denied.");

        var msgs = _data.State.ChatMessages
            .Where(m => m.TeacherId == p.TeacherId && m.ParentId == p.ParentId)
            .OrderBy(m => m.SentAt)
            .ToList();

        return new RpcResponse(req.RequestId, true, null, JsonExt.ToJsonElement(msgs));
    }
}

