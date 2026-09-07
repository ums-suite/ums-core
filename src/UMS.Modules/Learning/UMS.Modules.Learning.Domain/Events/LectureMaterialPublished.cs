using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-12: a new LectureMaterial was published (requirement-spec.md learning §3). Consumers: Notifications (enrolled Students), Reporting.</summary>
public sealed record LectureMaterialPublished(
    Guid LectureMaterialId,
    Guid CourseOfferingId,
    string MaterialType,
    string Title,
    Guid PublishedByUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
