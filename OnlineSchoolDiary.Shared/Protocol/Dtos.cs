using OnlineSchoolDiary.Shared.Models;

namespace OnlineSchoolDiary.Shared.Protocol;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(User User);

public sealed record CreateUserRequest(string Username, string Password, UserRole Role, string FullName, string? ClassId, Guid? ChildStudentId);
public sealed record UpdateUserRequest(Guid Id, string Username, UserRole Role, string FullName, string? ClassId, Guid? ChildStudentId);

public sealed record AssignTeacherRequest(Guid TeacherId, string ClassId, string SubjectId);

public sealed record SendNotificationRequest(string Title, string Message, NotificationAudience Audience, UserRole? TargetRole, string? TargetClassId, Guid? TargetUserId);

public sealed record UpsertDiaryRequest(Guid? Id, string ClassId, string SubjectId, DateOnly Date, string Title, string Text);

public sealed record AckDiaryRequest(Guid DiaryEntryId);

public sealed record SendChatMessageRequest(Guid TeacherId, Guid ParentId, string Text);
public sealed record GetChatRequest(Guid TeacherId, Guid ParentId);

public sealed record ReportsResponse(
    int TotalUsers,
    int TotalTeachers,
    int TotalStudents,
    int TotalParents,
    int TotalDiaryEntries,
    int TotalAcknowledgements
);

