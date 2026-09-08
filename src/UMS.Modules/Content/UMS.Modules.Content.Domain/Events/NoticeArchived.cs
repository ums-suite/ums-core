using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Domain.Events;

/// <summary>CNT-1: requirement-spec.md §3 - consumers: Reporting. Raised whether the transition was manual or `expire_at`-driven.</summary>
public sealed record NoticeArchived(Guid NoticeId, DateTimeOffset OccurredAt) : IDomainEvent;
