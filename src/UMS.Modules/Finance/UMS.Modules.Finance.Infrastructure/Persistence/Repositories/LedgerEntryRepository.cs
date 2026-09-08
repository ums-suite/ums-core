using Microsoft.EntityFrameworkCore;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Ledger;

namespace UMS.Modules.Finance.Infrastructure.Persistence.Repositories;

internal sealed class LedgerEntryRepository(FinanceDbContext context) : ILedgerEntryRepository
{
    public void Add(LedgerEntry entry) => context.LedgerEntries.Add(entry);

    public async Task<(IReadOnlyList<LedgerEntry> Items, int TotalCount)> ListAsync(LedgerEntryFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(context.LedgerEntries.AsNoTracking(), filter);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    private static IQueryable<LedgerEntry> ApplyFilter(IQueryable<LedgerEntry> query, LedgerEntryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.ReferenceType))
        {
            query = query.Where(e => e.ReferenceType == filter.ReferenceType);
        }

        if (filter.ReferenceId.HasValue)
        {
            query = query.Where(e => e.ReferenceId == filter.ReferenceId.Value);
        }

        if (filter.EntryType.HasValue)
        {
            query = query.Where(e => e.EntryType == filter.EntryType.Value);
        }

        if (filter.OccurredFrom.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= filter.OccurredFrom.Value);
        }

        if (filter.OccurredTo.HasValue)
        {
            query = query.Where(e => e.OccurredAt <= filter.OccurredTo.Value);
        }

        return query;
    }
}
