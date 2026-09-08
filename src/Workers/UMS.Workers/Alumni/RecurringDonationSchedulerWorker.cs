using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Donations;

namespace UMS.Workers.Alumni;

/// <summary>
/// ALM-10: Alumni's own recurring-donation scheduler (requirement-spec.md §2.4) - triggers a new
/// Finance Payment request for every recurring series due for its next cycle. Runs on a daily-scale
/// cadence (recurring donations are Monthly/Quarterly/Yearly at the coarsest, so a short poll interval
/// buys nothing) - no distributed lock needed for the same reason
/// <see cref="JobPostingExpirySweepWorker"/> needs none: a redundant concurrent tick against an
/// already-advanced <c>NextChargeAt</c> simply finds nothing left due.
/// </summary>
public sealed class RecurringDonationSchedulerWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringDonationSchedulerWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<RecurringDonationSchedulerService>();
                var correlationId = $"alumni-recurring-donation-scheduler-{Guid.NewGuid()}";
                var triggered = await service.TriggerDueCyclesAsync(BatchSize, correlationId, stoppingToken).ConfigureAwait(false);

                if (triggered > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Alumni recurring-donation scheduler: triggered {Triggered} due cycle(s).", triggered);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alumni recurring-donation scheduler: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
