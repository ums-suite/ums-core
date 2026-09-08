using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Donations;

namespace UMS.Workers.Alumni;

/// <summary>
/// ALM-9: polls Finance's own outbox for <c>PaymentSucceeded</c>/<c>PaymentFailed</c> and drives
/// <see cref="DonationConfirmationService"/> - mirrors Admission's own
/// <c>ApplicationPaymentConfirmationRelayWorker</c> exactly (design-decisions.md "Donation
/// Confirmation Consistency Model").
/// </summary>
public sealed class AlumniFinancePaymentRelayWorker(IServiceScopeFactory scopeFactory, ILogger<AlumniFinancePaymentRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

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
                logger.LogError(ex, "Alumni Finance-payment relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IFinancePaymentEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<DonationConfirmationService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            try
            {
                // ApplyAsync itself returns false (not a failure) for an event matching no Alumni
                // Donation - that belongs to a different module's own Finance usage and is marked
                // processed here too (nothing further for Alumni to do with it).
                await service.ApplyAsync(envelope.InvoiceId, envelope.EventType, $"AlumniFinancePaymentRelay:{envelope.EventId}", cancellationToken).ConfigureAwait(false);
                await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alumni Finance-payment relay: unhandled failure applying event {EventId}.", envelope.EventId);
            }
        }
    }
}
