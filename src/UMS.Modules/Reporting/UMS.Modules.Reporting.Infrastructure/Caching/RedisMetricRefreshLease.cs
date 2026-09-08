using StackExchange.Redis;
using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.Infrastructure.Caching;

/// <summary>
/// RPT-1/design-decisions.md "Job-Overlap Prevention Mechanism - Distributed Lease per
/// MetricRefreshJob". This is the EXACT SAME Redis distributed-lease primitive Admission's own
/// <c>RedisResultCache.TryAcquireRegenerationLockAsync</c> already uses in this codebase
/// (<c>IDatabase.LockTakeAsync</c>/<c>LockReleaseAsync</c>, a token-guarded lock so only the holder
/// can release its own lease) - reused here, not reinvented, per this build's explicit guidance:
/// Reporting is not standing up a new lock mechanism, it is applying the platform's existing
/// job-coordination primitive to a new key namespace (<c>reporting:metric-refresh-lease:*</c>).
/// Shares the platform's one <see cref="IConnectionMultiplexer"/> - never a second multiplexer.
/// </summary>
internal sealed class RedisMetricRefreshLease(IConnectionMultiplexer redis) : IMetricRefreshLease
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string metricKey, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var lockKey = $"reporting:metric-refresh-lease:{metricKey}";
        var token = Guid.NewGuid().ToString("N");
        var acquired = await db.LockTakeAsync(lockKey, token, ttl).ConfigureAwait(false);
        return acquired ? new RedisLockHandle(db, lockKey, token) : null;
    }

    private sealed class RedisLockHandle(IDatabase database, string lockKey, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await database.LockReleaseAsync(lockKey, token).ConfigureAwait(false);
    }
}
