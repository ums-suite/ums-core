using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Reservations;
using UMS.Modules.Library.Domain.Events;

namespace UMS.Workers.Library;

/// <summary>
/// LIB-9: design-decisions.md "Reservation-Queue Fairness and Claim Mechanism" - strictly
/// event-driven off <see cref="LoanReturned"/>, polling Library's OWN outbox (never a concurrent
/// polling sweep over BookCopy status directly, never inlined into the Return transaction itself) -
/// mirrors Hostel's own <c>HostelWaitlistReRankingRelayWorker</c> exactly.
/// </summary>
public sealed class LibraryReservationFulfillmentRelayWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryReservationFulfillmentRelayWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Library reservation-fulfillment relay: unexpected failure while processing pending LoanReturned events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var service = scope.ServiceProvider.GetRequiredService<ReservationFulfillmentService>();

        var messages = await outbox.GetUnprocessedAsync(nameof(LoanReturned), BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var loanReturned = JsonSerializer.Deserialize<LoanReturned>(message.PayloadJson) ?? throw new InvalidOperationException("Empty LoanReturned payload.");
                await service.FulfillAsync(loanReturned.BookCopyId, $"system:reservation-fulfillment:{message.Id}", cancellationToken).ConfigureAwait(false);
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Library reservation-fulfillment relay: failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
