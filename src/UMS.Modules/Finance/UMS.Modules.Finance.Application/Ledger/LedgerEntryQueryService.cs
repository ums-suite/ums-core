using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Ledger;

namespace UMS.Modules.Finance.Application.Ledger;

/// <summary>
/// FIN-13: <c>GET /api/v1/finance/ledger-entries</c> - Accountant/Reporting, read-only
/// (<c>finance.ledger.read</c>). Mirrors Audit's own <c>AuditQueryService.ListAsync</c> filtered-
/// listing shape exactly (requirement-spec.md §2: the Ledger "mirrors the append-only, tamper-evident
/// discipline ADR-0012 establishes for Audit" - applied here to its own read surface too, a
/// deliberate parallel, not a claim this IS an Audit read).
/// </summary>
public sealed class LedgerEntryQueryService(ILedgerEntryRepository repository)
{
    public async Task<LedgerEntryListPage> ListAsync(LedgerEntryFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var (items, total) = await repository.ListAsync(filter, skip, take, cancellationToken).ConfigureAwait(false);

        return new LedgerEntryListPage(items.Select(ToDto).ToList(), total, skip, take);
    }

    private static LedgerEntryDto ToDto(LedgerEntry entry) => new(
        entry.Id.Value,
        entry.EntryType.ToString(),
        entry.ReferenceType,
        entry.ReferenceId,
        entry.Amount,
        entry.Currency,
        entry.Description,
        entry.CorrelationId,
        entry.OccurredAt);
}
