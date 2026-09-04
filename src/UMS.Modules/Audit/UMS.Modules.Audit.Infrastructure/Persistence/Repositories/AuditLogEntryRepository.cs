using Microsoft.EntityFrameworkCore;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Domain.Entries;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Every query here goes against <see cref="AuditDbContext.Entries"/> - the partitioned parent
/// table - and never a per-partition child table (edge-cases.md's boundary-spanning-query
/// decision), so a query spanning a monthly partition boundary is handled transparently by
/// PostgreSQL's own partition-pruning/merge-append plan.
/// </summary>
internal sealed class AuditLogEntryRepository(AuditDbContext context) : IAuditLogEntryRepository
{
    public Task<AuditLogEntry?> GetByIdAsync(AuditLogEntryId id, CancellationToken cancellationToken = default) =>
        context.Entries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> ListAsync(
        AuditEntryFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(context.Entries.AsNoTracking(), filter);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Ordered by the monotonic ULID id (design-decisions.md's Ordering/Sequencing Mechanism),
        // newest first - occurred_at alone cannot break a same-millisecond tie consistently.
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAllAsync(AuditEntryFilter filter, CancellationToken cancellationToken = default) =>
        await ApplyFilter(context.Entries.AsNoTracking(), filter)
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AuditLogEntry>> GetEntityHistoryAsync(string entityType, string entityId, CancellationToken cancellationToken = default) =>
        await context.Entries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static IQueryable<AuditLogEntry> ApplyFilter(IQueryable<AuditLogEntry> query, AuditEntryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(e => e.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(e => e.EntityId == filter.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorId))
        {
            query = query.Where(e => e.ActorId == filter.ActorId);
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(e => e.OccurredAt <= filter.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var actionResult = AuditAction.Create(filter.Action);
            if (actionResult.IsSuccess)
            {
                query = query.Where(e => e.Action == actionResult.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Application))
        {
            query = query.Where(e => e.Application == filter.Application);
        }

        return query;
    }
}
