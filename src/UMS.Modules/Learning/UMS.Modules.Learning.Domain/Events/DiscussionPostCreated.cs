using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-17: a post or reply was added (requirement-spec.md learning §3). Consumers: Notifications (thread participants).</summary>
public sealed record DiscussionPostCreated(
    Guid DiscussionPostId,
    Guid DiscussionThreadId,
    Guid CourseOfferingId,
    Guid AuthorUserId,
    Guid? ParentPostId,
    DateTimeOffset OccurredAt) : IDomainEvent;
