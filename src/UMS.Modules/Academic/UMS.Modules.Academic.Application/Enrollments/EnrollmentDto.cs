namespace UMS.Modules.Academic.Application.Enrollments;

public sealed record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    Guid CourseOfferingId,
    Guid SemesterId,
    Guid SectionId,
    string Status,
    int CreditHours,
    string? PrerequisiteOverrideReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? DroppedAt);

public sealed record CreateEnrollmentRequest(Guid CourseOfferingId, Guid SectionId, string? PrerequisiteOverrideReason);

public sealed record DropEnrollmentRequest(string? Reason);
