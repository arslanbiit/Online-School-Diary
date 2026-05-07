using OnlineSchoolDiary.Shared.Models;

namespace OnlineSchoolDiary.Server.Services;

public sealed class AppState
{
    public List<User> Users { get; } = new();
    public List<ClassRoom> Classes { get; } = new();
    public List<Subject> Subjects { get; } = new();
    public List<TeacherAssignment> Assignments { get; } = new();
    public List<DiaryEntry> DiaryEntries { get; } = new();
    public List<DiaryAcknowledgement> Acks { get; } = new();
    public List<Notification> Notifications { get; } = new();
    public List<ChatMessage> ChatMessages { get; } = new();
}

