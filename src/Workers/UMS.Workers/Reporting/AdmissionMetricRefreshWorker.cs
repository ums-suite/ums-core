using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.DashboardMetrics;

namespace UMS.Workers.Reporting;

/// <summary>
/// RPT-1/RPT-5: refreshes the Admission dashboard on a cadence that self-adjusts to campaign state
/// (requirement-spec.md §2.2 Admission row; edge-cases.md "An admission campaign starts or ends
/// mid-cycle") - near-real-time (10 minutes) while any campaign is active, nightly (24 hours)
/// otherwise. <see cref="AdmissionDashboardRefreshService.DetermineNextPollIntervalAsync"/> is
/// called after every run to compute the NEXT tick's own delay, so this worker's own loop never
/// hard-codes a single interval the way every other dashboard's own refresh worker does.
/// </summary>
public sealed class AdmissionMetricRefreshWorker(IServiceScopeFactory scopeFactory, ILogger<AdmissionMetricRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentInterval = AdmissionDashboardRefreshService.NightlyInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AdmissionDashboardRefreshService>();
                await service.RunAsync(stoppingToken).ConfigureAwait(false);
                currentInterval = await service.DetermineNextPollIntervalAsync(currentInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admission dashboard refresh: unexpected failure during a scheduled run - retaining current interval {CurrentInterval}.", currentInterval);
            }

            await Task.Delay(currentInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
