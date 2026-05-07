using OnlineSchoolDiary.Server.Infrastructure;
using OnlineSchoolDiary.Shared.Models;
using OnlineSchoolDiary.Shared.Security;

namespace OnlineSchoolDiary.Server.Services;

public sealed class DataService
{
    private readonly JsonFileStore _store;
    private readonly AppState _state;

    private const string UsersKey = "users";
    private const string ClassesKey = "classes";
    private const string SubjectsKey = "subjects";
    private const string AssignmentsKey = "assignments";
    private const string DiaryKey = "diaryEntries";
    private const string AcksKey = "diaryAcks";
    private const string NotificationsKey = "notifications";
    private const string ChatKey = "chatMessages";

    public DataService(JsonFileStore store, AppState state)
    {
        _store = store;
        _state = state;
    }

    public AppState State => _state;

    public async Task InitializeAsync()
    {
        _state.Users.Clear();
        _state.Users.AddRange(await _store.LoadListAsync<User>(UsersKey));

        _state.Classes.Clear();
        _state.Classes.AddRange(await _store.LoadListAsync<ClassRoom>(ClassesKey));

        _state.Subjects.Clear();
        _state.Subjects.AddRange(await _store.LoadListAsync<Subject>(SubjectsKey));

        _state.Assignments.Clear();
        _state.Assignments.AddRange(await _store.LoadListAsync<TeacherAssignment>(AssignmentsKey));

        _state.DiaryEntries.Clear();
        _state.DiaryEntries.AddRange(await _store.LoadListAsync<DiaryEntry>(DiaryKey));

        _state.Acks.Clear();
        _state.Acks.AddRange(await _store.LoadListAsync<DiaryAcknowledgement>(AcksKey));

        _state.Notifications.Clear();
        _state.Notifications.AddRange(await _store.LoadListAsync<Notification>(NotificationsKey));

        _state.ChatMessages.Clear();
        _state.ChatMessages.AddRange(await _store.LoadListAsync<ChatMessage>(ChatKey));

        if (_state.Classes.Count == 0)
        {
            _state.Classes.AddRange(new[]
            {
                new ClassRoom("3", "3rd Class"),
                new ClassRoom("4", "4th Class"),
                new ClassRoom("5", "5th Class")
            });
        }

        if (_state.Subjects.Count == 0)
        {
            _state.Subjects.AddRange(new[]
            {
                new Subject("eng", "English"),
                new Subject("math", "Math"),
                new Subject("sci", "Science")
            });
        }

        if (_state.Users.Count == 0)
        {
            var studentId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();

            _state.Users.Add(new User(Guid.NewGuid(), "admin", PasswordHasher.Hash("admin"), UserRole.Admin, "System Admin", null, null));
            _state.Users.Add(new User(teacherId, "ali", PasswordHasher.Hash("123"), UserRole.Teacher, "Sir Ali", null, null));
            _state.Users.Add(new User(studentId, "student1", PasswordHasher.Hash("123"), UserRole.Student, "Student One", "3", null));
            _state.Users.Add(new User(parentId, "parent1", PasswordHasher.Hash("123"), UserRole.Parent, "Parent One", null, studentId));

            _state.Assignments.Add(new TeacherAssignment(Guid.NewGuid(), teacherId, "3", "eng"));
            _state.Assignments.Add(new TeacherAssignment(Guid.NewGuid(), teacherId, "4", "math"));
        }

        await PersistAllAsync();
    }

    public async Task PersistAllAsync()
    {
        await _store.SaveListAsync(UsersKey, _state.Users);
        await _store.SaveListAsync(ClassesKey, _state.Classes);
        await _store.SaveListAsync(SubjectsKey, _state.Subjects);
        await _store.SaveListAsync(AssignmentsKey, _state.Assignments);
        await _store.SaveListAsync(DiaryKey, _state.DiaryEntries);
        await _store.SaveListAsync(AcksKey, _state.Acks);
        await _store.SaveListAsync(NotificationsKey, _state.Notifications);
        await _store.SaveListAsync(ChatKey, _state.ChatMessages);
    }
}

