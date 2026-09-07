using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-18: an Instructor removed or restored a post (requirement-spec.md learning §3). Consumers: Audit - which this module ALSO writes to synchronously, in the same transaction as the transition itself (§5 Auditability), rather than relying on this fan-out alone.</summary>
public sealed record DiscussionPostModerated(
    Guid DiscussionPostId,
    Guid DiscussionThreadId,
    Guid CourseOfferingId,
    string PreviousModerationStatus,
    string NewModerationStatus,
    Guid ModeratedByUserId,
    string? Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
