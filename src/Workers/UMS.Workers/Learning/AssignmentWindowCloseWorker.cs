using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Learning.Application.Assignments;

namespace UMS.Workers.Learning;

/// <summary>
/// LRN-2/LRN-9: the <c>hardCloseAt</c> sweep - closes elapsed Assignment windows (raising
/// <c>AssignmentClosed</c>) and re-enqueues a <c>PlagiarismCheck</c> for any counted Submission
/// still lacking a completed one, per edge-cases.md's own residual note on the window-close retry.
/// Mirrors Documents' <c>PendingDocumentSweepWorker</c>'s compensating-sweep shape.
/// </summary>
public sealed class AssignmentWindowCloseWorker(IServiceScopeFactory scopeFactory, ILogger<AssignmentWindowCloseWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;

    // A minute-scale interval, not a second-scale one: nothing here is latency-sensitive (an
    // Assignment closing a few seconds after hardCloseAt has no effect on any Submission's own
    // accept check, which is evaluated per-request against the window itself, not against the
    // Assignment's status flag).
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AssignmentWindowCloseService>();

                var closed = await service.CloseElapsedWindowsAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (closed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Assignment window-close sweep: closed {Closed} Assignment(s) whose hardCloseAt had passed.", closed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Assignment window-close sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
