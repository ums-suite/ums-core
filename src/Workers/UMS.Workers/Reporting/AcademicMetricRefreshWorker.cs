using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.DashboardMetrics;

namespace UMS.Workers.Reporting;

/// <summary>RPT-1/RPT-4: nightly refresh of the Academic dashboard (requirement-spec.md §2.2 Academic row) - one independent <see cref="BackgroundService"/> per dashboard family (design-decisions.md: no single mega-scheduler for six jobs whose only shared shape is "acquire a lease, compute a payload, upsert one row"), mirroring <c>UMS.Workers.Finance.ReconciliationWorker</c>'s own simple periodic-poll shape.</summary>
public sealed class AcademicMetricRefreshWorker(IServiceScopeFactory scopeFactory, ILogger<AcademicMetricRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AcademicDashboardRefreshService>();
                await service.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Academic dashboard refresh: unexpected failure during a scheduled run.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
