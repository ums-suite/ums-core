namespace UMS.Modules.Academic.Application.ResultPublications;

public sealed record ResultPublicationDto(
    Guid Id,
    Guid CourseOfferingId,
    string Status,
    DateTimeOffset? CalculatedAt,
    DateTimeOffset? RejectedAt,
    string? RejectionReason,
    DateTimeOffset? LockedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    int CorrectionCount);

public sealed record RejectGradeBatchRequest(string Reason);
