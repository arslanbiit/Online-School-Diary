namespace OnlineSchoolDiary.Shared.Models;

public sealed record Notification(
    Guid Id,
    Guid SenderUserId,
    string Title,
    string Message,
    NotificationAudience Audience,
    UserRole? TargetRole,
    string? TargetClassId,
    Guid? TargetUserId,
    DateTimeOffset SentAt
);

