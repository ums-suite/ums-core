using UMS.Modules.Reporting.Domain.DashboardMetrics;

namespace UMS.Modules.Reporting.UnitTests.DashboardMetrics;

/// <summary>RPT-1/RPT-10: the two invariants requirement-spec.md §4 requires be structurally true, not merely convention.</summary>
public sealed class DashboardMetricTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_freshly_created_metric_has_never_been_computed()
    {
        var metric = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);

        Assert.False(metric.HasEverBeenComputed);
        Assert.Null(metric.PayloadJson);
        Assert.Null(metric.DataAsOf);
    }

    [Fact]
    public void Create_rejects_a_blank_key()
    {
        Assert.Throws<ArgumentException>(() => DashboardMetric.Create("   ", "Academic Dashboard", Now));
    }

    [Fact]
    public void A_successful_refresh_sets_payload_and_data_as_of_together()
    {
        var metric = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);

        metric.RecordSuccessfulRefresh("{\"total\":10}", Now);

        Assert.True(metric.HasEverBeenComputed);
        Assert.Equal("{\"total\":10}", metric.PayloadJson);
        Assert.Equal(Now, metric.DataAsOf);
        Assert.True(metric.LastRefreshSucceeded);
        Assert.Null(metric.LastRefreshError);
    }

    [Fact]
    public void A_failed_refresh_after_a_prior_success_retains_the_old_payload_and_data_as_of_untouched()
    {
        var metric = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);
        metric.RecordSuccessfulRefresh("{\"total\":10}", Now);

        var laterAttempt = Now.AddHours(24);
        metric.RecordFailedRefresh("source module unavailable", laterAttempt);

        // requirement-spec.md §4: retain-and-alert - never fabricate a zero/default, never advance
        // data_as_of past the last SUCCESSFUL run.
        Assert.Equal("{\"total\":10}", metric.PayloadJson);
        Assert.Equal(Now, metric.DataAsOf);
        Assert.False(metric.LastRefreshSucceeded);
        Assert.Equal("source module unavailable", metric.LastRefreshError);
        Assert.Equal(laterAttempt, metric.LastRefreshAttemptedAt);
    }

    [Fact]
    public void A_failed_refresh_with_no_prior_success_leaves_the_metric_never_computed()
    {
        var metric = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);

        metric.RecordFailedRefresh("source module unavailable", Now);

        Assert.False(metric.HasEverBeenComputed);
        Assert.Null(metric.PayloadJson);
        Assert.Null(metric.DataAsOf);
    }
}
