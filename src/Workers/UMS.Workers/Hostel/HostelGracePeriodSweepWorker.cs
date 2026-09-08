using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Allocations;

namespace UMS.Workers.Hostel;

/// <summary>HOS-10: requirement-spec.md §9 decision 3 - the fee-payment grace-period auto-expiry scheduled job (design-decisions.md "Fee-Grace-Period Expiry Mechanism": a self-contained TTL sweep, no query-back to Finance).</summary>
public sealed class HostelGracePeriodSweepWorker(IServiceScopeFactory scopeFactory, ILogger<HostelGracePeriodSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<GracePeriodExpiryService>();
                var expired = await service.SweepAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (expired > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Hostel grace-period sweep: expired {Count} unpaid Allocation(s) back to the bed pool.", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hostel grace-period sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
