namespace UMS.Modules.Finance.Application.Ledger;

public sealed record LedgerEntryDto(
    Guid Id,
    string EntryType,
    string ReferenceType,
    Guid ReferenceId,
    decimal Amount,
    string Currency,
    string Description,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record LedgerEntryListPage(IReadOnlyList<LedgerEntryDto> Items, int TotalCount, int Skip, int Take);
