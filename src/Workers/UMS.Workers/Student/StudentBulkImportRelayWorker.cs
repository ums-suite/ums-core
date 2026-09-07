using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.BulkImport;

namespace UMS.Workers.Student;

/// <summary>
/// STU-15 (requirement-spec.md student §2 Bulk Import, ADR-0014): drives every
/// <c>Approved</c>/resumed-<c>Processing</c> <see cref="Domain.BulkImport.StudentBulkImportJob"/>
/// forward one bounded batch at a time via <see cref="StudentBulkImportProcessingService"/>,
/// resuming from each job's own row-level checkpoint on crash/restart - mirrors Admission's own
/// <c>PublishJobRelayWorker</c>/Documents' <c>BulkGenerationRelayWorker</c> shape exactly.
/// </summary>
public sealed class StudentBulkImportRelayWorker(IServiceScopeFactory scopeFactory, ILogger<StudentBulkImportRelayWorker> logger) : BackgroundService
{
    private const int JobBatchSize = 10;
    private const int RowBatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

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
                logger.LogError(ex, "Student bulk-import relay: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessActiveJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IStudentBulkImportJobRepository>();
        var service = scope.ServiceProvider.GetRequiredService<StudentBulkImportProcessingService>();

        var activeJobIds = await jobs.GetActiveAsync(JobBatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var jobId in activeJobIds)
        {
            var result = await service.ProcessNextBatchAsync(jobId.Value, RowBatchSize, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure && logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError("Student bulk-import job {JobId} batch processing failed: {Error}.", jobId.Value, result.Error);
            }
        }
    }
}
