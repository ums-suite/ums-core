using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-3: a CourseOffering is opened for registration (requirement-spec.md §3 Domain events table).</summary>
public sealed record CourseOfferingPublished(Guid CourseOfferingId, Guid CourseId, Guid SemesterId, DateTimeOffset OccurredAt) : IDomainEvent;
