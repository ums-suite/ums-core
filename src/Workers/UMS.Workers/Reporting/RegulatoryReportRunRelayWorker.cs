using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.RegulatoryReports;

namespace UMS.Workers.Reporting;

/// <summary>
/// RPT-12/RPT-13: polls for <c>Pending</c> <c>RegulatoryReportRun</c> rows and drives each one
/// through <see cref="RegulatoryReportRunExecutionService"/> - the mechanism behind "every
/// <c>POST .../run</c> is fully async, never inline generation" (requirement-spec.md §4). Mirrors
/// Documents' own bulk-generation job polling shape (see
/// <c>RegulatoryReportRunExecutionService</c>'s own class remarks) rather than a redundant parallel
/// outbox entry pointing at the same row.
/// </summary>
public sealed class RegulatoryReportRunRelayWorker(IServiceScopeFactory scopeFactory, ILogger<RegulatoryReportRunRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runs = scope.ServiceProvider.GetRequiredService<IRegulatoryReportRunRepository>();
                var executionService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunExecutionService>();

                var pending = await runs.GetPendingAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                foreach (var run in pending)
                {
                    try
                    {
                        await executionService.ExecuteAsync(run, stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // ExecuteAsync already persists a Failed transition for any expected failure
                        // mode - reaching here means the failure happened before/around that persist
                        // step itself. Logged and left for the next poll pass, mirroring every other
                        // relay worker's own "one bad row never stops the batch" posture.
                        logger.LogError(ex, "RegulatoryReportRun {RunId}: relay execution failed unexpectedly - will be retried next poll.", run.Id);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "RegulatoryReportRun relay: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
