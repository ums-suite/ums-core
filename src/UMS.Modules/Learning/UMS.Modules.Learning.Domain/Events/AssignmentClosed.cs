using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-2: the window's <c>hardCloseAt</c> was reached, or an Instructor closed the Assignment manually (requirement-spec.md learning §3). Consumers: Notifications, Reporting - and this module's own <c>hardCloseAt</c> plagiarism-recheck sweep (§9.4).</summary>
public sealed record AssignmentClosed(Guid AssignmentId, Guid CourseOfferingId, Guid InstructorUserId, DateTimeOffset OccurredAt) : IDomainEvent;
