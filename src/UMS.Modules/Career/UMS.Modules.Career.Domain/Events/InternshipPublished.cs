using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: `Draft -> Published`. Consumers: Notifications (digest to eligible Students), Reporting.</summary>
public sealed record InternshipPublished(Guid InternshipId, DateTimeOffset OccurredAt) : IDomainEvent;
