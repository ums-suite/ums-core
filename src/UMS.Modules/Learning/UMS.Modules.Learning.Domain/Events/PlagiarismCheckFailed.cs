using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-8: the Polly pipeline's retries were exhausted or its circuit was open (requirement-spec.md learning §3). Consumers: Notifications (Instructor), Reporting. Surfaced as "could not be checked" - never silently treated as clean, and never blocking evaluation.</summary>
public sealed record PlagiarismCheckFailed(
    Guid PlagiarismCheckId,
    Guid SubmissionId,
    Guid AssignmentId,
    string Reason,
    int AttemptCount,
    Guid InstructorUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
