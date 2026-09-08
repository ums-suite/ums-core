using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Fines;

namespace UMS.Workers.Library;

/// <summary>LIB-11: the daily per-day-rate Fine-accrual sweep - design-decisions.md "Fine-Accrual Job Idempotency".</summary>
public sealed class LibraryFineAccrualSweepWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryFineAccrualSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<FineAccrualService>();
                var accrued = await service.AccrueAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (accrued > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Library fine-accrual sweep: accrued {Count} daily Fine increment(s).", accrued);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Library fine-accrual sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
