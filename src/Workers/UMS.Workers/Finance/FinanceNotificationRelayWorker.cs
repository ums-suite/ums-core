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
/// Scope, stated explicitly: requirement-spec.md §6 also names <c>RefundCompleted</c> as a
/// consumed event, but Refund has no write path in this build's Payment Core slice (release/
/// DEVELOPMENT_PLAN.md Flow #14) - it is reserved for the remainder Finance pass, Flow #18, along
/// with that event's own relay wiring.
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
