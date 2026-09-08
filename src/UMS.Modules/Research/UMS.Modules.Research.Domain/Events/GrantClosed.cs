using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

/// <summary>requirement-spec.md §3/§7: consumed by Reporting; Notifications fan-out to PI/Co-Is.</summary>
public sealed record GrantClosed(Guid GrantId, DateTimeOffset OccurredAt) : IDomainEvent;
