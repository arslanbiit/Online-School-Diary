namespace OnlineSchoolDiary.Shared.Models;

public sealed record ChatMessage(
    Guid Id,
    Guid TeacherId,
    Guid ParentId,
    Guid SenderId,
    string Text,
    DateTimeOffset SentAt
);

