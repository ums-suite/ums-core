using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Events;

/// <summary>requirement-spec.md §3: raised when a posting clears moderation, or auto-publishes (alumnus poster). Consumers: Notifications (job digest, ALM-15), Reporting.</summary>
public sealed record JobPostingPublished(Guid JobPostingId, DateTimeOffset OccurredAt) : IDomainEvent;
