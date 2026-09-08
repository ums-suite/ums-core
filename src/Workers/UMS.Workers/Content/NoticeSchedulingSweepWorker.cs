using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Scheduling;

namespace UMS.Workers.Content;

/// <summary>
/// CNT-4: the `publish_at &lt;= now AND status = Scheduled` / `expire_at &lt;= now AND status =
/// Published` scan-and-transition - requirement-spec.md §4's "driven by a background job, never a
/// request-time side effect." design-decisions.md "Scheduled-Publish Job Exactly-Once Execution
/// Mechanism": NO distributed lock/lease here (unlike Reporting's `MetricRefreshJobBase`) - safe to
/// run from multiple concurrent <c>UMS.Workers</c> replicas because <see cref="NoticeSchedulingService"/>'s
/// own optimistic-concurrency write is what makes a redundant concurrent tick harmless.
/// </summary>
public sealed class NoticeSchedulingSweepWorker(IServiceScopeFactory scopeFactory, ILogger<NoticeSchedulingSweepWorker> logger) : BackgroundService
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
                var service = scope.ServiceProvider.GetRequiredService<NoticeSchedulingService>();
                var correlationId = $"content-notice-sweep-{Guid.NewGuid()}";

                var published = await service.PublishDueAsync(BatchSize, correlationId, stoppingToken).ConfigureAwait(false);
                var archived = await service.ExpireDueAsync(BatchSize, correlationId, stoppingToken).ConfigureAwait(false);

                if ((published > 0 || archived > 0) && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Content notice sweep: published {Published}, archived {Archived}.", published, archived);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Content notice sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
