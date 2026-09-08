using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Fines;

namespace UMS.Workers.Library;

/// <summary>LIB-13: mirrors Admission's/Hostel's own Finance-payment relay exactly - polls Finance's outbox for <c>PaymentSucceeded</c>/<c>PaymentFailed</c>, filtered to Library's own Fines by InvoiceId.</summary>
public sealed class LibraryFinancePaymentRelayWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryFinancePaymentRelayWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Library Finance-payment relay: unexpected failure while processing pending Finance payment events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IFinancePaymentEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<FinePaymentConfirmationService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            try
            {
                await service.ApplyAsync(envelope.InvoiceId, envelope.EventType, cancellationToken).ConfigureAwait(false);
                await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Library Finance-payment relay: unhandled failure applying event {EventId} ({EventType}).", envelope.EventId, envelope.EventType);
            }
        }
    }
}
