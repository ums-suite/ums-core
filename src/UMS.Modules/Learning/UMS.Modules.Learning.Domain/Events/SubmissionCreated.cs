using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-6: a Student submitted (first attempt or resubmission) (requirement-spec.md learning §3). Consumers: Audit, and this module's own PlagiarismCheck trigger (§9.4).</summary>
public sealed record SubmissionCreated(
    Guid SubmissionId,
    Guid AssignmentId,
    Guid CourseOfferingId,
    Guid StudentId,
    Guid SubmittedByUserId,
    DateTimeOffset SubmittedAt,
    bool IsLate,
    decimal LatePenaltyPercentage,
    DateTimeOffset OccurredAt) : IDomainEvent;
