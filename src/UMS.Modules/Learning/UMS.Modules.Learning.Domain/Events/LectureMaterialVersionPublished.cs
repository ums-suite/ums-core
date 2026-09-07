using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.Events;

/// <summary>LRN-14: a new version was added to an existing LectureMaterial (requirement-spec.md learning §3). Consumers: Notifications (enrolled Students). Every prior version stays individually addressable.</summary>
public sealed record LectureMaterialVersionPublished(
    Guid LectureMaterialId,
    Guid LectureMaterialVersionId,
    Guid CourseOfferingId,
    int VersionNumber,
    Guid PublishedByUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;
