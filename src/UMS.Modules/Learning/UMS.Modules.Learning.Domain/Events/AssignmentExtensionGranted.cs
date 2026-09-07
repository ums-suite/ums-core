using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-3: an Instructor grants a per-Student <c>SubmissionExtension</c> (requirement-spec.md learning §3). Consumers: Audit, Notifications (that Student only).</summary>
public sealed record AssignmentExtensionGranted(
    Guid AssignmentId,
    Guid SubmissionExtensionId,
    Guid StudentId,
    Guid StudentUserId,
    DateTimeOffset ExtendedDeadline,
    Guid GrantedByUserId,
    bool WaivesLatePenalty,
    DateTimeOffset OccurredAt) : IDomainEvent;
