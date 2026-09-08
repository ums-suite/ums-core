using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

/// <summary>design-decisions.md "Publication Duplicate Detection and Merge Mechanism" - raised only by an explicit Admin <c>POST .../publications/{id}/merge</c>, never automatically.</summary>
public sealed record PublicationsMerged(Guid SurvivingPublicationId, Guid MergedPublicationId, DateTimeOffset OccurredAt) : IDomainEvent;
