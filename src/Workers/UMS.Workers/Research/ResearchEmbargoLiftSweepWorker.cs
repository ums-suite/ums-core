using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.InstitutionalRepositoryEntries;

namespace UMS.Workers.Research;

/// <summary>
/// RES-12/ADR-0014: the daily lapsed-embargo sweep - design-decisions.md "InstitutionalRepositoryEntry
/// Embargo-Lift Mechanism" deliberately picks a scheduled worker over lazy read-time evaluation
/// specifically to keep the public showcase's own `GET .../public/repository-entries` genuinely
/// HTTP-cacheable (§5 Caching row) - a read-time check would make every read a write path.
/// Mirrors Finance's <c>ReconciliationWorker</c>'s own daily-interval shape exactly (this codebase's
/// established "no separate cron/quartz infrastructure" precedent).
/// </summary>
public sealed class ResearchEmbargoLiftSweepWorker(IServiceScopeFactory scopeFactory, ILogger<ResearchEmbargoLiftSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<EmbargoLiftService>();
                var correlationId = $"research-embargo-lift-sweep-{Guid.NewGuid()}";

                var lifted = await service.LiftLapsedEmbargoesAsync(BatchSize, correlationId, stoppingToken).ConfigureAwait(false);
                if (lifted > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Research embargo-lift sweep: lifted {Lifted} lapsed embargo(es).", lifted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Research embargo-lift sweep: unexpected failure during a daily run.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
