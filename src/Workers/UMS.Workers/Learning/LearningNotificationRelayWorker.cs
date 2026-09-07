using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Events;

namespace UMS.Workers.Learning;

/// <summary>
/// Fans Learning's own outbox events out through <see cref="INotificationRequestPublisher"/>
/// (ADR-0009: "never a direct email/SMS/push call") - mirroring Faculty's
/// <c>LeaveNotificationRelayWorker</c> exactly.
///
/// <para>
/// <b>Scope, stated explicitly rather than silently narrowed:</b> this relay handles the four
/// requirement-spec.md §3 events with a single, unambiguous recipient this module already holds -
/// <c>AssignmentExtensionGranted</c> (that Student), <c>SubmissionEvaluated</c> (that Student),
/// and <c>PlagiarismCheckCompleted</c>/<c>PlagiarismCheckFailed</c> (that Assignment's Instructor).
/// The class-wide fan-outs §3 also lists - <c>AssignmentPublished</c>,
/// <c>LectureMaterialPublished</c>, <c>LectureMaterialVersionPublished</c>,
/// <c>DiscussionPostCreated</c> - need a CourseOffering's full enrolled-Student ROSTER resolved to
/// Identity user ids, and no shared contract exposes that today
/// (<c>UMS.Shared.Academic.ICourseOfferingLookup</c> answers membership per-caller, not
/// enumeration). Those events are still published to the outbox and remain available to Reporting;
/// only their notification fan-out is deferred, flagged in this module's PR as a known gap rather
/// than worked around with a forbidden direct Student/Academic schema read.
/// </para>
/// </summary>
public sealed class LearningNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<LearningNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string[] HandledEventTypes =
    [
        nameof(AssignmentExtensionGranted),
        nameof(SubmissionEvaluated),
        nameof(PlagiarismCheckCompleted),
        nameof(PlagiarismCheckFailed),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var eventType in HandledEventTypes)
                {
                    await ProcessPendingAsync(eventType, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Learning notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static LearningNotificationRequest? ToRequest(string eventType, string payloadJson, Guid outboxMessageId) => eventType switch
    {
        nameof(AssignmentExtensionGranted) => FromExtensionGranted(Deserialize<AssignmentExtensionGranted>(payloadJson), outboxMessageId),
        nameof(SubmissionEvaluated) => FromSubmissionEvaluated(Deserialize<SubmissionEvaluated>(payloadJson), outboxMessageId),
        nameof(PlagiarismCheckCompleted) => FromPlagiarismCompleted(Deserialize<PlagiarismCheckCompleted>(payloadJson), outboxMessageId),
        nameof(PlagiarismCheckFailed) => FromPlagiarismFailed(Deserialize<PlagiarismCheckFailed>(payloadJson), outboxMessageId),
        _ => throw new InvalidOperationException($"Unknown Learning notification event type '{eventType}'."),
    };

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    private static LearningNotificationRequest? FromExtensionGranted(AssignmentExtensionGranted evt, Guid outboxMessageId) =>
        evt.StudentUserId == Guid.Empty
            ? null
            : new LearningNotificationRequest(
                evt.StudentUserId,
                nameof(AssignmentExtensionGranted),
                evt.SubmissionExtensionId.ToString(),
                new Dictionary<string, string>
                {
                    ["assignmentId"] = evt.AssignmentId.ToString(),
                    ["extendedDeadline"] = evt.ExtendedDeadline.ToString("O", CultureInfo.InvariantCulture),
                    ["waivesLatePenalty"] = evt.WaivesLatePenalty.ToString(CultureInfo.InvariantCulture),
                },
                outboxMessageId.ToString());

    private static LearningNotificationRequest? FromSubmissionEvaluated(SubmissionEvaluated evt, Guid outboxMessageId) =>
        evt.StudentUserId == Guid.Empty
            ? null
            : new LearningNotificationRequest(
                evt.StudentUserId,
                nameof(SubmissionEvaluated),
                evt.SubmissionId.ToString(),
                new Dictionary<string, string>
                {
                    ["assignmentId"] = evt.AssignmentId.ToString(),
                    ["awardedPoints"] = evt.AwardedPoints.ToString(CultureInfo.InvariantCulture),
                    ["maxPoints"] = evt.MaxPoints.ToString(CultureInfo.InvariantCulture),
                },
                outboxMessageId.ToString());

    private static LearningNotificationRequest? FromPlagiarismCompleted(PlagiarismCheckCompleted evt, Guid outboxMessageId) =>
        evt.InstructorUserId == Guid.Empty
            ? null
            : new LearningNotificationRequest(
                evt.InstructorUserId,
                nameof(PlagiarismCheckCompleted),
                evt.PlagiarismCheckId.ToString(),
                new Dictionary<string, string>
                {
                    ["submissionId"] = evt.SubmissionId.ToString(),
                    ["similarityPercentage"] = evt.SimilarityPercentage.ToString(CultureInfo.InvariantCulture),
                    ["provider"] = evt.ProviderName,
                },
                outboxMessageId.ToString());

    private static LearningNotificationRequest? FromPlagiarismFailed(PlagiarismCheckFailed evt, Guid outboxMessageId) =>
        evt.InstructorUserId == Guid.Empty
            ? null
            : new LearningNotificationRequest(
                evt.InstructorUserId,
                nameof(PlagiarismCheckFailed),
                evt.PlagiarismCheckId.ToString(),
                new Dictionary<string, string>
                {
                    ["submissionId"] = evt.SubmissionId.ToString(),
                    ["reason"] = evt.Reason,
                    ["attemptCount"] = evt.AttemptCount.ToString(CultureInfo.InvariantCulture),
                },
                outboxMessageId.ToString());

    private async Task ProcessPendingAsync(string eventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationRequestPublisher>();

        var messages = await outbox.GetUnprocessedAsync(eventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var request = ToRequest(eventType, message.PayloadJson, message.Id);
                if (request is not null)
                {
                    await publisher.PublishAsync(request, cancellationToken).ConfigureAwait(false);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A Notifications outage must never re-queue or block the underlying Learning
                // mutation, which already committed - only this best-effort fan-out retries.
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Learning notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
