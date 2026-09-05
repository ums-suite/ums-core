using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Events;

/// <summary>ACD-11: the `Published` transition - named directly in ADR-0003's own example list (requirement-spec.md §3). On Academic's 100%-trace-coverage result-publication path (ums-conventions.md, Observability) - every emission of this event must carry `correlationId` + `resultPublicationId` on its log line.</summary>
public sealed record GradeLocked(Guid ResultPublicationId, Guid CourseOfferingId, Guid LockedByUserId, DateTimeOffset OccurredAt) : IDomainEvent;
