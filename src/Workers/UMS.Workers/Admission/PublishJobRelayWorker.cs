using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Results;

namespace UMS.Workers.Admission;

/// <summary>
/// design-decisions.md "Write-Through Cache Publish Atomicity"/"Background Job Design for Bulk
/// Admit-Card and Result-Notification Generation": drives every active <see cref="Domain.Publishing.PublishJob"/>
/// forward one bounded batch at a time via <see cref="PublishJobService"/>, resuming from each job's
/// own checkpoint on crash/restart. A short poll interval - result-day is exactly when this worker's
/// throughput matters most (requirement-spec.md §5's up-to-100,000-applicant traffic model).
/// </summary>
public sealed class PublishJobRelayWorker(IServiceScopeFactory scopeFactory, ILogger<PublishJobRelayWorker> logger) : BackgroundService
{
    private const int JobBatchSize = 20;
    private const int BatchSize = 200;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessActiveJobsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admission PublishJob relay: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessActiveJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IPublishJobRepository>();
        var service = scope.ServiceProvider.GetRequiredService<PublishJobService>();

        var activeJobIds = await jobs.GetActiveAsync(JobBatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var jobId in activeJobIds)
        {
            var result = await service.ProcessNextBatchAsync(jobId.Value, BatchSize, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure && logger.IsEnabled(LogLevel.Error))
            {
                // design-decisions.md's elevated retry/DLQ tier: a stalled/failed PublishJob pages
                // on-call directly - logged at Error as the signal an alerting rule watches for.
                logger.LogError("Admission PublishJob {PublishJobId} batch processing failed: {Error}.", jobId.Value, result.Error);
            }
        }
    }
}
