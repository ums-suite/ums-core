using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Events;

/// <summary>requirement-spec.md §3: raised by the ALM-6 scheduled expiry sweep when <c>expires_at</c> is reached. Consumer: Reporting.</summary>
public sealed record JobPostingExpired(Guid JobPostingId, DateTimeOffset OccurredAt) : IDomainEvent;
