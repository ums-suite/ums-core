using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Documents.Application.Generation;

namespace UMS.Workers.BulkDocumentGeneration;

/// <summary>
/// edge-cases.md's object-storage/DB-ordering decision, residual note on the compensating cleanup
/// job's own schedule - runs <see cref="PendingDocumentSweepService"/> on a fixed interval rather
/// than in response to an outbox message, since there is no single triggering event for "a claim
/// row has been stuck below Ready for too long."
/// </summary>
public sealed class PendingDocumentSweepWorker(IServiceScopeFactory scopeFactory, ILogger<PendingDocumentSweepWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sweepService = scope.ServiceProvider.GetRequiredService<PendingDocumentSweepService>();
                var swept = await sweepService.SweepAsync(stoppingToken).ConfigureAwait(false);

                if (swept > 0)
                {
                    logger.LogWarning("Pending document sweep: {Count} stale claim(s) marked Failed and their orphaned artifacts cleaned up.", swept);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Pending document sweep: unexpected failure.");
            }

            await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
