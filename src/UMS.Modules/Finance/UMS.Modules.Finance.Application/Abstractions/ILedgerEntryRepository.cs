using UMS.Modules.Finance.Domain.Ledger;

namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>design-decisions.md "Ledger-Entry Atomicity Mechanism": <see cref="Add"/> only for writes - there is deliberately no Update/Delete method anywhere on this interface (requirement-spec.md §4's append-only invariant).</summary>
public interface ILedgerEntryRepository
{
    public void Add(LedgerEntry entry);

    /// <summary>FIN-13: <c>GET /api/v1/finance/ledger-entries</c>'s own filtered/paged read - mirrors Audit's own <c>IAuditLogEntryRepository.ListAsync</c> filtered-listing shape exactly.</summary>
    public Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> ListAsync(LedgerEntryFilter filter, int skip, int take, CancellationToken cancellationToken = default);
}

public sealed record LedgerEntryFilter(string? ReferenceType, Guid? ReferenceId, LedgerEntryType? EntryType, DateTimeOffset? OccurredFrom, DateTimeOffset? OccurredTo);
