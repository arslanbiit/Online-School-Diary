namespace OnlineSchoolDiary.Shared.Models;

public sealed record DiaryEntry(
    Guid Id,
    string ClassId,
    string SubjectId,
    Guid TeacherId,
    DateOnly Date,
    string Title,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record DiaryAcknowledgement(
    Guid Id,
    Guid DiaryEntryId,
    Guid StudentId,
    DateTimeOffset AcknowledgedAt
);

