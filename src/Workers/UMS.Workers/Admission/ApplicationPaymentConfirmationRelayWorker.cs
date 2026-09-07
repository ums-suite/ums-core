using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Applications;

namespace UMS.Workers.Admission;

/// <summary>design-decisions.md "Idempotency for Application Submission (Including Duplicate Payment-Webhook Delivery)": polls Finance's own outbox (<see cref="IFinancePaymentEventSource"/>) for <c>PaymentSucceeded</c>/<c>PaymentFailed</c> and applies each to Admission's own Application state machine via <see cref="ApplicationPaymentConfirmationService"/>.</summary>
public sealed class ApplicationPaymentConfirmationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<ApplicationPaymentConfirmationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admission payment-confirmation relay: unexpected failure while processing pending Finance payment events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IFinancePaymentEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<ApplicationPaymentConfirmationService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            try
            {
                await service.ApplyAsync(envelope.InvoiceId, envelope.EventType, $"system:finance-payment-relay:{envelope.EventId}", cancellationToken).ConfigureAwait(false);
                await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admission payment-confirmation relay: unhandled failure applying event {EventId} ({EventType}).", envelope.EventId, envelope.EventType);
            }
        }
    }
}
