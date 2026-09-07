using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-1: `Draft -> Published` (requirement-spec.md learning §3). Consumers: Notifications (enrolled Students), Reporting.</summary>
public sealed record AssignmentPublished(
    Guid AssignmentId,
    Guid CourseOfferingId,
    string Title,
    DateTimeOffset Deadline,
    Guid InstructorUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
