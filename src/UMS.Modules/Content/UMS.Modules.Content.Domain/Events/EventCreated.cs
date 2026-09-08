using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Domain.Events;

/// <summary>CNT-7: requirement-spec.md §3 - consumer: Reporting.</summary>
public sealed record EventCreated(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
