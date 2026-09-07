using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

public sealed record LedgerEntryPosted(Guid LedgerEntryId, string EntryType, string ReferenceType, Guid ReferenceId, decimal Amount, DateTimeOffset OccurredAt) : IDomainEvent;
