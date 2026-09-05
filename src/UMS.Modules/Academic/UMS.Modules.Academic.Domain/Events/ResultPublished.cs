using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-12: `ResultPublication` reaches `Published` - named directly in ADR-0003's own example list (requirement-spec.md §3). 100%-trace-coverage path - see <see cref="GradeLocked"/>'s own remarks.</summary>
public sealed record ResultPublished(Guid ResultPublicationId, Guid CourseOfferingId, Guid PublishedByUserId, DateTimeOffset OccurredAt) : IDomainEvent;
