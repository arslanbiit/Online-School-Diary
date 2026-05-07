using OnlineSchoolDiary.Shared.Models;

namespace OnlineSchoolDiary.Client.Infrastructure;

public sealed class AppSession
{
    public required RpcClient Rpc { get; init; }
    public required string Token { get; init; }
    public required User User { get; init; }

    public List<ClassRoom> Classes { get; } = new();
    public List<Subject> Subjects { get; } = new();
    public List<TeacherAssignment> Assignments { get; } = new();

    public string ClassName(string? classId) =>
        classId is null ? "" : Classes.FirstOrDefault(c => c.Id == classId)?.Name ?? classId;

    public string SubjectName(string subjectId) =>
        Subjects.FirstOrDefault(s => s.Id == subjectId)?.Name ?? subjectId;
}

