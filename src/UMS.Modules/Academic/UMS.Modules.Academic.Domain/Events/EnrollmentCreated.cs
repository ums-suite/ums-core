using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-6: the atomic 4-check validation passed (requirement-spec.md §3). Fan-out only - no synchronous Finance dependency (§7, §9 decision 5).</summary>
public sealed record EnrollmentCreated(Guid EnrollmentId, Guid StudentId, Guid CourseOfferingId, Guid SemesterId, DateTimeOffset OccurredAt) : IDomainEvent;
