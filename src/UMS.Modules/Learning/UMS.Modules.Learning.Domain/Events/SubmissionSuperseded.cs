using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-7: a resubmission superseded a prior Submission (requirement-spec.md learning §3). Consumers: Audit.</summary>
public sealed record SubmissionSuperseded(
    Guid SubmissionId,
    Guid SupersededBySubmissionId,
    Guid AssignmentId,
    Guid StudentId,
    DateTimeOffset OccurredAt) : IDomainEvent;
