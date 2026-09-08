using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Reporting.UnitTests.Fakes;

namespace UMS.Modules.Reporting.UnitTests.DashboardMetrics;

/// <summary>RPT-1/RPT-10: the shared job-base mechanics every one of the six dashboard refresh services inherits - lease-skip, retry/backoff, and retain-on-exhaustion, exercised in isolation from any real source-module query or Redis.</summary>
public sealed class MetricRefreshJobBaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_successful_first_attempt_upserts_the_metric_and_releases_the_lease()
    {
        var lease = new FakeMetricRefreshLease();
        var metrics = new FakeDashboardMetricRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(Now);
        var job = new TestMetricRefreshJob(lease, metrics, unitOfWork, clock, NullLogger.Instance, new Queue<Func<string>>([() => "{\"total\":42}"]));

        await job.RunAsync();

        Assert.Equal(1, job.ComputeCallCount);
        var metric = await metrics.GetByKeyAsync("test-dashboard");
        Assert.NotNull(metric);
        Assert.True(metric!.HasEverBeenComputed);
        Assert.Equal("{\"total\":42}", metric.PayloadJson);
        Assert.Equal(Now, metric.DataAsOf);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);

        // The lease must be released once the run completes - a later tick can acquire it again.
        var reacquired = await lease.TryAcquireAsync("test-dashboard", TimeSpan.FromMinutes(10));
        Assert.NotNull(reacquired);
    }

    [Fact]
    public async Task RunAsync_requests_the_lease_under_this_job_s_own_MetricKey_and_LeaseTtl()
    {
        var lease = new FakeMetricRefreshLease();
        var job = new TestMetricRefreshJob(
            lease,
            new FakeDashboardMetricRepository(),
            new FakeUnitOfWork(),
            new FakeClock(Now),
            NullLogger.Instance,
            new Queue<Func<string>>([() => "{}"]),
            leaseTtl: TimeSpan.FromMinutes(37));

        await job.RunAsync();

        Assert.Equal("test-dashboard", lease.LastRequestedKey);
        Assert.Equal(TimeSpan.FromMinutes(37), lease.LastRequestedTtl);
    }

    [Fact]
    public async Task A_tick_that_cannot_acquire_the_lease_is_skipped_entirely_never_queued_or_retried()
    {
        var lease = new FakeMetricRefreshLease();
        await using var externalHold = lease.HoldExternally("test-dashboard");

        var metrics = new FakeDashboardMetricRepository();
        var unitOfWork = new FakeUnitOfWork();
        var job = new TestMetricRefreshJob(lease, metrics, unitOfWork, new FakeClock(Now), NullLogger.Instance, new Queue<Func<string>>([() => "{}"]));

        await job.RunAsync();

        // edge-cases.md "A scheduled MetricRefreshJob still running when the next scheduled run
        // fires" - this tick does no work at all.
        Assert.Equal(0, job.ComputeCallCount);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
        Assert.Null(await metrics.GetByKeyAsync("test-dashboard"));
    }

    [Fact]
    public async Task A_transient_failure_that_recovers_within_the_retry_budget_still_succeeds()
    {
        var lease = new FakeMetricRefreshLease();
        var metrics = new FakeDashboardMetricRepository();
        var job = new TestMetricRefreshJob(
            lease,
            metrics,
            new FakeUnitOfWork(),
            new FakeClock(Now),
            NullLogger.Instance,
            new Queue<Func<string>>(
            [
                () => throw new InvalidOperationException("source module transiently unavailable"),
                () => "{\"total\":7}",
            ]));

        await job.RunAsync();

        Assert.Equal(2, job.ComputeCallCount);
        var metric = await metrics.GetByKeyAsync("test-dashboard");
        Assert.True(metric!.HasEverBeenComputed);
        Assert.Equal("{\"total\":7}", metric.PayloadJson);
        Assert.True(metric.LastRefreshSucceeded);
    }

    [Fact]
    public async Task Exhausting_the_retry_budget_retains_the_prior_value_and_never_fabricates_a_default()
    {
        var lease = new FakeMetricRefreshLease();
        var metrics = new FakeDashboardMetricRepository();
        var clock = new FakeClock(Now);

        // Seed a metric with a prior successful value, as if an earlier tick had already computed one.
        var firstJob = new TestMetricRefreshJob(lease, metrics, new FakeUnitOfWork(), clock, NullLogger.Instance, new Queue<Func<string>>([() => "{\"total\":100}"]));
        await firstJob.RunAsync();

        // Every attempt of the NEXT run fails - three strikes exhausts MetricRefreshJobBase's own
        // MaxAttempts (a real, if short, retry/backoff delay is exercised here, not mocked away).
        clock.UtcNow = Now.AddHours(24);
        var failingJob = new TestMetricRefreshJob(
            lease,
            metrics,
            new FakeUnitOfWork(),
            clock,
            NullLogger.Instance,
            new Queue<Func<string>>(
            [
                () => throw new InvalidOperationException("down"),
                () => throw new InvalidOperationException("down"),
                () => throw new InvalidOperationException("down"),
            ]));

        await failingJob.RunAsync();

        Assert.Equal(3, failingJob.ComputeCallCount);
        var metric = await metrics.GetByKeyAsync("test-dashboard");
        Assert.NotNull(metric);

        // requirement-spec.md §4 retain-and-alert: the PRIOR successful payload/data_as_of survive
        // completely untouched by the exhausted retry.
        Assert.Equal("{\"total\":100}", metric!.PayloadJson);
        Assert.Equal(Now, metric.DataAsOf);
        Assert.False(metric.LastRefreshSucceeded);
        Assert.Equal(Now.AddHours(24), metric.LastRefreshAttemptedAt);
    }
}
