using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

/// <summary>requirement-spec.md §3 Domain Events - Grant lifecycle. Consumers: Reporting.</summary>
public sealed record GrantProposed(Guid GrantId, Guid FundingBodyId, DateTimeOffset OccurredAt) : IDomainEvent;
