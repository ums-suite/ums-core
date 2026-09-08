using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Reconciliation;

namespace UMS.Workers.Finance;

/// <summary>
/// FIN-14/ADR-0014: the daily reconciliation background job. Mirrors
/// <see cref="StuckPaymentSweepWorker"/>'s own poll-loop shape exactly - a simple periodic
/// <see cref="BackgroundService"/>, the same "no separate cron/quartz infrastructure" precedent every
/// other scheduled job in this codebase already follows, rather than a real calendar-aware scheduler.
///
/// <para>
/// Processes "yesterday" (UTC) as its settlement date on each run - a Payment that settles in the
/// last few minutes before midnight is picked up by the FOLLOWING day's run instead (design-
/// decisions.md "Reconciliation Job Concurrency-Safety": "acceptable since §2/§8 never state
/// reconciliation must be same-day for every transaction, only that it runs daily").
/// </para>
/// </summary>
public sealed class ReconciliationWorker(IServiceScopeFactory scopeFactory, ILogger<ReconciliationWorker> logger) : BackgroundService
{
    private const int BatchSize = 200;

    // A day-scale interval - ADR-0014's own "daily scheduled background job", not a real cron
    // scheduler (no such infrastructure exists in this codebase - StuckPaymentSweepWorker's own
    // simple periodic-poll shape is this platform's established precedent for a scheduled job).
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settlementDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ReconciliationService>();

                var summary = await service.RunAsync(settlementDate, BatchSize, stoppingToken).ConfigureAwait(false);
                if (summary.Candidates > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Finance reconciliation: settlement date {SettlementDate} - {Candidates} candidate(s), {Reconciled} reconciled, {Flagged} flagged for review.",
                        settlementDate,
                        summary.Candidates,
                        summary.Reconciled,
                        summary.Flagged);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Finance reconciliation: unexpected failure during a daily run.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
