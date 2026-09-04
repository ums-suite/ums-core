using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.Events;

namespace UMS.Workers.Faculty;

/// <summary>FAC-12: polls Faculty's own outbox for `LeaveApproved`/`LeaveRejected` and fans them out through <see cref="INotificationRequestPublisher"/> (ADR-0009) - mirrors Documents' own notification-publishing relay pattern.</summary>
public sealed class LeaveNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<LeaveNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly string ApprovedEventType = typeof(LeaveApproved).FullName ?? nameof(LeaveApproved);
    private static readonly string RejectedEventType = typeof(LeaveRejected).FullName ?? nameof(LeaveRejected);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(ApprovedEventType, "LeaveApproved", stoppingToken).ConfigureAwait(false);
                await ProcessPendingAsync(RejectedEventType, "LeaveRejected", stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Leave notification relay: unexpected failure while processing pending LeaveApproved/LeaveRejected events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static (Guid RecipientUserId, string LeaveRequestId, IReadOnlyDictionary<string, string> MergeFields) FromApproved(LeaveApproved evt) =>
        (evt.RequesterUserId, evt.LeaveRequestId.ToString(), new Dictionary<string, string> { ["leaveRequestId"] = evt.LeaveRequestId.ToString() });

    private static (Guid RecipientUserId, string LeaveRequestId, IReadOnlyDictionary<string, string> MergeFields) FromRejected(LeaveRejected evt) =>
        (evt.RequesterUserId, evt.LeaveRequestId.ToString(), new Dictionary<string, string> { ["leaveRequestId"] = evt.LeaveRequestId.ToString(), ["reason"] = evt.Reason ?? string.Empty });

    private async Task ProcessPendingAsync(string outboxEventType, string notificationEventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationRequestPublisher>();

        var messages = await outbox.GetUnprocessedAsync(outboxEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            await ProcessOneAsync(outbox, publisher, message, notificationEventType, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(IOutboxReader outbox, INotificationRequestPublisher publisher, OutboxMessageDto message, string notificationEventType, CancellationToken cancellationToken)
    {
        try
        {
            var (recipientUserId, leaveRequestId, mergeFields) = notificationEventType switch
            {
                "LeaveApproved" => FromApproved(JsonSerializer.Deserialize<LeaveApproved>(message.PayloadJson) ?? throw new InvalidOperationException("Empty LeaveApproved payload.")),
                "LeaveRejected" => FromRejected(JsonSerializer.Deserialize<LeaveRejected>(message.PayloadJson) ?? throw new InvalidOperationException("Empty LeaveRejected payload.")),
                _ => throw new InvalidOperationException($"Unknown notification event type '{notificationEventType}'."),
            };

            await publisher.PublishAsync(new NotificationRequest(recipientUserId, notificationEventType, leaveRequestId, mergeFields, message.Id.ToString()), cancellationToken).ConfigureAwait(false);
            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // FAC-12: a Notifications outage must never re-queue or block the underlying
            // LeaveRequest decision, which already committed - only this best-effort fan-out
            // retries (design-decisions.md's Audit boundary decision draws the same line for the
            // business mutation itself, which is unaffected here).
            await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
            logger.LogWarning(ex, "Leave notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
        }
    }
}
