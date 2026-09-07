using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-2: an Instructor/Registrar cancelled the Assignment, e.g. because its CourseOffering was cancelled (requirement-spec.md learning §3/§8). Consumers: Audit, Notifications, Reporting.</summary>
public sealed record AssignmentCancelled(Guid AssignmentId, Guid CourseOfferingId, string Reason, Guid InstructorUserId, DateTimeOffset OccurredAt) : IDomainEvent;
