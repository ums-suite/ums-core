using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Scheduling;

namespace UMS.Workers.Content;

/// <summary>CNT-9: the identical idempotent, lock-free sweep as <see cref="NoticeSchedulingSweepWorker"/>, reused for Banner.</summary>
public sealed class BannerSchedulingSweepWorker(IServiceScopeFactory scopeFactory, ILogger<BannerSchedulingSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<BannerSchedulingService>();
                var correlationId = $"content-banner-sweep-{Guid.NewGuid()}";

                var published = await service.PublishDueAsync(BatchSize, correlationId, stoppingToken).ConfigureAwait(false);
                var archived = await service.ExpireDueAsync(BatchSize, correlationId, stoppingToken).ConfigureAwait(false);

                if ((published > 0 || archived > 0) && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Content banner sweep: published {Published}, archived {Archived}.", published, archived);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Content banner sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
