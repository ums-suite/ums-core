namespace UMS.Modules.Reporting.Application.Abstractions;

/// <summary>
/// RPT-1/design-decisions.md "Job-Overlap Prevention Mechanism": a distributed lease per
/// <c>MetricRefreshJob</c>, reusing Admission's own existing Redis lease primitive
/// (<c>RedisResultCache.TryAcquireRegenerationLockAsync</c>) rather than inventing a new lock
/// mechanism - see <c>Infrastructure.Caching.RedisMetricRefreshLease</c>'s own remarks.
///
/// <para>
/// A tick that fails to acquire the lease is SKIPPED outright - not queued, not retried
/// immediately (edge-cases.md "A scheduled MetricRefreshJob still running when the next scheduled
/// run fires"). <see cref="MetricRefreshJobBase"/> is the one caller of this interface.
/// </para>
/// </summary>
public interface IMetricRefreshLease
{
    public Task<IAsyncDisposable?> TryAcquireAsync(string metricKey, TimeSpan ttl, CancellationToken cancellationToken = default);
}
