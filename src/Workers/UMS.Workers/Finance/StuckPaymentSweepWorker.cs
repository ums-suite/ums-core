using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Payments;

namespace UMS.Workers.Finance;

/// <summary>FIN-9/FIN-10: the stuck-payment background sweep - see <see cref="StuckPaymentSweepService"/>'s own remarks. Mirrors Documents' own <c>PendingDocumentSweepWorker</c> compensating-sweep shape.</summary>
public sealed class StuckPaymentSweepWorker(IServiceScopeFactory scopeFactory, ILogger<StuckPaymentSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;

    // A minute-scale poll interval, not a second-scale one: FIN-9/FIN-10's own thresholds are
    // 10/30-minute scale, so nothing here is latency-sensitive.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<StuckPaymentSweepService>();

                var processed = await service.SweepAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (processed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Finance stuck-payment sweep: processed {Processed} Payment(s).", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Finance stuck-payment sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
