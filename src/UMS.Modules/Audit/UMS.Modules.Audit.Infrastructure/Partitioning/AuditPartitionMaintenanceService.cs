using Microsoft.EntityFrameworkCore;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Infrastructure.Persistence;

namespace UMS.Modules.Audit.Infrastructure.Partitioning;

/// <summary>
/// AUD-3: ensures every monthly partition <c>audit.audit_log_entries</c> needs for the current
/// operating window already exists, run once at Host startup (see
/// <c>DependencyInjection.UseAuditModuleAsync</c>) - a few months ahead so a clock/deploy-timing
/// edge case never lands a write with no partition yet to receive it (Postgres raises a hard error
/// on an insert with no matching partition, which per ADR-0012's atomicity invariant would fail
/// the *caller's* whole transaction, not just Audit's).
/// </summary>
internal sealed class AuditPartitionMaintenanceService(AuditDbContext context, IClock clock)
{
    public async Task EnsureFuturePartitionsAsync(int monthsBehind = 1, int monthsAhead = 3, CancellationToken cancellationToken = default)
    {
        foreach (var (name, rangeStart, rangeEndExclusive) in PartitionNaming.RequiredPartitions(clock.UtcNow, monthsBehind, monthsAhead))
        {
            var sql = $"""
                CREATE TABLE IF NOT EXISTS audit.{name}
                PARTITION OF {PartitionNaming.ParentTable}
                FOR VALUES FROM ('{rangeStart:O}') TO ('{rangeEndExclusive:O}');
                """;

            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
        }
    }
}
