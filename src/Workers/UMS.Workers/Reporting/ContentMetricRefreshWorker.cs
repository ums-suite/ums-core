using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.DashboardMetrics;

namespace UMS.Workers.Reporting;

/// <summary>Flow #26: nightly refresh of the seventh, Content-owned admin dashboard - one independent <see cref="BackgroundService"/> per dashboard family, mirroring <see cref="AcademicMetricRefreshWorker"/>'s own exact shape.</summary>
public sealed class ContentMetricRefreshWorker(IServiceScopeFactory scopeFactory, ILogger<ContentMetricRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ContentDashboardRefreshService>();
                await service.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Content dashboard refresh: unexpected failure during a scheduled run.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
