using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Events;

namespace UMS.Workers.Finance;

/// <summary>
/// FIN-16: fans Finance's own outbox events out through <see cref="INotificationRequestPublisher"/>
/// (ADR-0009: "never a direct email/SMS/push call") - mirroring Learning's/Faculty's own
/// NotificationRelayWorker exactly.
///
/// <para>
/// FIN-11 wires in the last event this worker's own remarks had previously flagged as deferred:
/// requirement-spec.md §6 names <c>RefundCompleted</c> as consumed by Notifications (alongside the
/// calling module and Reporting, neither of which is this worker's job) - now that Refund has a real
/// write path (Flow #18's remainder Finance pass), it fans out here identically to
/// <c>PaymentSucceeded</c>/<c>PaymentFailed</c> above it. <c>PaymentReconciled</c> is deliberately
/// NOT added here - §6 names it as consumed by Reporting only, not Notifications.
/// </para>
/// </summary>
public sealed class FinanceNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<FinanceNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string[] HandledEventTypes =
    [
        nameof(InvoiceGenerated),
        nameof(PaymentSucceeded),
        nameof(PaymentFailed),
        nameof(RefundCompleted),
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
                logger.LogError(ex, "Finance notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static FinanceNotificationRequest ToRequest(string eventType, string payloadJson, Guid outboxMessageId) => eventType switch
    {
        nameof(InvoiceGenerated) => FromInvoiceGenerated(Deserialize<InvoiceGenerated>(payloadJson), outboxMessageId),
        nameof(PaymentSucceeded) => FromPaymentSucceeded(Deserialize<PaymentSucceeded>(payloadJson), outboxMessageId),
        nameof(PaymentFailed) => FromPaymentFailed(Deserialize<PaymentFailed>(payloadJson), outboxMessageId),
        nameof(RefundCompleted) => FromRefundCompleted(Deserialize<RefundCompleted>(payloadJson), outboxMessageId),
        _ => throw new InvalidOperationException($"Unknown Finance notification event type '{eventType}'."),
    };

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    private static FinanceNotificationRequest FromInvoiceGenerated(InvoiceGenerated evt, Guid outboxMessageId) =>
        new(
            evt.OwnerId,
            nameof(InvoiceGenerated),
            evt.InvoiceId.ToString(),
            new Dictionary<string, string>
            {
                ["feeType"] = evt.FeeType,
                ["totalAmount"] = evt.TotalAmount.ToString("F2", CultureInfo.InvariantCulture),
                ["sourceModule"] = evt.SourceModule,
            },
            outboxMessageId.ToString());

    private static FinanceNotificationRequest FromPaymentSucceeded(PaymentSucceeded evt, Guid outboxMessageId) =>
        new(
            evt.OwnerId,
            nameof(PaymentSucceeded),
            evt.PaymentId.ToString(),
            new Dictionary<string, string>
            {
                ["invoiceId"] = evt.InvoiceId.ToString(),
                ["amount"] = evt.Amount.ToString("F2", CultureInfo.InvariantCulture),
            },
            outboxMessageId.ToString());

    private static FinanceNotificationRequest FromPaymentFailed(PaymentFailed evt, Guid outboxMessageId) =>
        new(
            evt.OwnerId,
            nameof(PaymentFailed),
            evt.PaymentId.ToString(),
            new Dictionary<string, string>
            {
                ["invoiceId"] = evt.InvoiceId.ToString(),
                ["reason"] = evt.Reason,
            },
            outboxMessageId.ToString());

    private static FinanceNotificationRequest FromRefundCompleted(RefundCompleted evt, Guid outboxMessageId) =>
        new(
            evt.OwnerId,
            nameof(RefundCompleted),
            evt.RefundId.ToString(),
            new Dictionary<string, string>
            {
                ["paymentId"] = evt.PaymentId.ToString(),
                ["invoiceId"] = evt.InvoiceId.ToString(),
                ["amount"] = evt.Amount.ToString("F2", CultureInfo.InvariantCulture),
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
                await publisher.PublishAsync(request, cancellationToken).ConfigureAwait(false);
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A Notifications outage must never re-queue or block the underlying Finance
                // mutation, which already committed - only this best-effort fan-out retries.
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Finance notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
