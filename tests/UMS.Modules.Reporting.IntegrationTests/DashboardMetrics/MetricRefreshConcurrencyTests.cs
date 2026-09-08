using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.DashboardMetrics;
using UMS.Modules.Reporting.IntegrationTests.Infrastructure;

namespace UMS.Modules.Reporting.IntegrationTests.DashboardMetrics;

/// <summary>
/// RPT-1/design-decisions.md "Job-Overlap Prevention Mechanism": the genuine race this whole
/// mechanism exists to prevent, proven against a REAL Redis (Testcontainers) with a real
/// <see cref="Task.WhenAll(Task[])"/> - not two sequential calls that happen to never overlap.
/// Mirrors Admission's own <c>ResultCacheTests.The_regeneration_lock_is_exclusive_until_released</c>
/// but exercised through the actual dashboard refresh service, end to end.
/// </summary>
[Collection(ReportingTestCollectionDefinition.Name)]
public sealed class MetricRefreshConcurrencyTests(ReportingServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_ticks_of_the_same_dashboard_never_both_run_the_underlying_query()
    {
        // A deliberately slow source-module call keeps the lease held long enough for the second,
        // truly-concurrent tick to observe it as already-taken.
        fixture.AcademicQuery.Delay = TimeSpan.FromSeconds(2);

        // The fixture (and its fake query's own call counter) is shared across every test class in
        // this collection - measure the DELTA this test itself causes, never an absolute count.
        var callCountBefore = fixture.AcademicQuery.CallCount;

        async Task<int> RunOnceAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AcademicDashboardRefreshService>();
            await service.RunAsync();
            return 1;
        }

        try
        {
            await Task.WhenAll(RunOnceAsync(), RunOnceAsync());

            // Exactly one of the two overlapping ticks actually acquired the lease and called the
            // source-module query - the other was skipped outright (never queued, never retried).
            Assert.Equal(1, fixture.AcademicQuery.CallCount - callCountBefore);
        }
        finally
        {
            // Never leak the injected delay into a later test sharing this same fixture instance.
            fixture.AcademicQuery.Delay = TimeSpan.Zero;
        }

        using var readScope = fixture.Services.CreateScope();
        var metrics = readScope.ServiceProvider.GetRequiredService<IDashboardMetricRepository>();
        var metric = await metrics.GetByKeyAsync(AcademicDashboardRefreshService.MetricKeyValue);
        Assert.NotNull(metric);
        Assert.True(metric!.HasEverBeenComputed);
    }

    [Fact]
    public async Task Once_a_run_completes_and_releases_its_lease_the_next_tick_can_acquire_it_again()
    {
        fixture.AcademicQuery.Delay = TimeSpan.Zero;

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<AcademicDashboardRefreshService>();
            await service.RunAsync();
        }

        var callCountAfterFirstRun = fixture.AcademicQuery.CallCount;

        using (var scope = fixture.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<AcademicDashboardRefreshService>();
            await service.RunAsync();
        }

        Assert.Equal(callCountAfterFirstRun + 1, fixture.AcademicQuery.CallCount);
    }
}
