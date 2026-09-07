using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.PlagiarismChecks;
using UMS.Modules.Learning.Domain.Events;

namespace UMS.Workers.Learning;

/// <summary>
/// LRN-8: drains Learning's own outbox for <see cref="SubmissionCreated"/> to enqueue a
/// <c>PlagiarismCheck</c>, then drives every queued check through the Polly-wrapped provider - the
/// same outbox-relay-worker shape Faculty's <c>LeaveNotificationRelayWorker</c> and Documents'
/// bulk-generation relay already use.
///
/// <para>
/// Triggering here rather than inline in the submit request is exactly what design-decisions.md's
/// "PlagiarismCheck Execution Timing &amp; Resilience" requires: a Student's submit must never wait
/// on a metered third-party call, and a check whose Submission is superseded before it completes is
/// cancelled rather than run (<c>Submission.Supersede</c> does that transition; this worker simply
/// finds nothing left to run).
/// </para>
/// </summary>
public sealed class PlagiarismCheckDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<PlagiarismCheckDispatchWorker> logger) : BackgroundService
{
    private const int BatchSize = 25;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly string SubmissionCreatedEventType = nameof(SubmissionCreated);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnqueuePendingChecksAsync(stoppingToken).ConfigureAwait(false);
                await RunQueuedChecksAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Plagiarism-check dispatch: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task EnqueuePendingChecksAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var checks = scope.ServiceProvider.GetRequiredService<PlagiarismCheckService>();

        var messages = await outbox.GetUnprocessedAsync(SubmissionCreatedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<SubmissionCreated>(message.PayloadJson)
                    ?? throw new InvalidOperationException("Empty SubmissionCreated payload.");

                var enqueued = await checks.EnqueueForSubmissionAsync(payload.SubmissionId, cancellationToken).ConfigureAwait(false);
                if (enqueued.IsFailure && logger.IsEnabled(LogLevel.Information))
                {
                    // A Submission that already has a live or completed check, or one superseded
                    // between the event being written and this pass - both are correct outcomes,
                    // not retryable failures, so the message is still acked below.
                    logger.LogInformation(
                        "Plagiarism-check dispatch: nothing to enqueue for submission {SubmissionId} ({Code}).",
                        payload.SubmissionId,
                        enqueued.Error!.Code);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Plagiarism-check dispatch: could not enqueue a check for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }

    private async Task RunQueuedChecksAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var checks = scope.ServiceProvider.GetRequiredService<PlagiarismCheckService>();

        var processed = await checks.RunQueuedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        if (processed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Plagiarism-check dispatch: drove {Processed} check(s) to a terminal outcome.", processed);
        }
    }
}
