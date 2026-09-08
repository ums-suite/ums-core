using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Allocations;

namespace UMS.Workers.Hostel;

/// <summary>HOS-9: mirrors Admission's own <c>ApplicationPaymentConfirmationRelayWorker</c> exactly - polls Finance's outbox for <c>PaymentSucceeded</c>/<c>PaymentFailed</c>, filtered to Hostel's own Allocations by InvoiceId.</summary>
public sealed class HostelFinancePaymentRelayWorker(IServiceScopeFactory scopeFactory, ILogger<HostelFinancePaymentRelayWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Hostel Finance-payment relay: unexpected failure while processing pending Finance payment events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IFinancePaymentEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<AllocationFeeConfirmationService>();

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
                logger.LogError(ex, "Hostel Finance-payment relay: unhandled failure applying event {EventId} ({EventType}).", envelope.EventId, envelope.EventType);
            }
        }
    }
}
