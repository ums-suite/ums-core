using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-7: within the drop window, seat released in the same transaction as the status change (requirement-spec.md §3).</summary>
public sealed record EnrollmentDropped(Guid EnrollmentId, Guid StudentId, Guid CourseOfferingId, string? Reason, DateTimeOffset OccurredAt) : IDomainEvent;
