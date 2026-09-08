using UMS.Modules.Reporting.Application.DashboardMetrics;
using UMS.Modules.Reporting.Domain.DashboardMetrics;
using UMS.Modules.Reporting.UnitTests.Fakes;

namespace UMS.Modules.Reporting.UnitTests.DashboardMetrics;

public sealed class DashboardMetricReadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_dashboard_that_has_never_been_refreshed_returns_the_explicit_NeverComputed_shape()
    {
        var service = new DashboardMetricReadService(new FakeDashboardMetricRepository());

        var response = await service.GetAsync("academic-dashboard");

        Assert.Equal(DashboardMetricComputationStatus.NeverComputed, response.Status);
        Assert.Null(response.DataAsOf);
        Assert.Null(response.Payload);
    }

    [Fact]
    public async Task A_computed_dashboard_returns_its_payload_and_data_as_of()
    {
        var repository = new FakeDashboardMetricRepository();
        var metric = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);
        metric.RecordSuccessfulRefresh("{\"totalEnrollments\":500}", Now);
        repository.Add(metric);

        var service = new DashboardMetricReadService(repository);
        var response = await service.GetAsync("academic-dashboard");

        Assert.Equal(DashboardMetricComputationStatus.Computed, response.Status);
        Assert.Equal(Now, response.DataAsOf);
        Assert.NotNull(response.Payload);
        Assert.Equal(500, response.Payload!.Value.GetProperty("totalEnrollments").GetInt32());
    }

    [Fact]
    public async Task A_row_that_exists_but_was_created_and_never_successfully_refreshed_is_still_NeverComputed()
    {
        var repository = new FakeDashboardMetricRepository();
        var metric = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);
        metric.RecordFailedRefresh("boom", Now);
        repository.Add(metric);

        var service = new DashboardMetricReadService(repository);
        var response = await service.GetAsync("academic-dashboard");

        Assert.Equal(DashboardMetricComputationStatus.NeverComputed, response.Status);
    }
}
