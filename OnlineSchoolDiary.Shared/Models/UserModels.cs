namespace OnlineSchoolDiary.Shared.Models;

public sealed record User(
    Guid Id,
    string Username,
    string PasswordHash,
    UserRole Role,
    string FullName,
    string? ClassId,
    Guid? ChildStudentId
);

public sealed record ClassRoom(string Id, string Name);

public sealed record Subject(string Id, string Name);

public sealed record TeacherAssignment(
    Guid Id,
    Guid TeacherId,
    string ClassId,
    string SubjectId
);

