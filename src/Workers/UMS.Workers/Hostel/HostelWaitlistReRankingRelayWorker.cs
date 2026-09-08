using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Domain.Events;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Workers.Hostel;

/// <summary>
/// HOS-14: design-decisions.md "Waitlist Re-Ranking Consistency Mechanism" - strictly event-driven
/// off <see cref="AllocationCheckedOut"/>/<see cref="AllocationExpired"/>, polling Hostel's OWN
/// outbox (never a concurrent poll over Bed status directly, never inlined into the freeing
/// transaction). Runs only after the freeing transaction has already committed - by construction,
/// since the event cannot exist in the outbox until that transaction commits.
/// </summary>
public sealed class HostelWaitlistReRankingRelayWorker(IServiceScopeFactory scopeFactory, ILogger<HostelWaitlistReRankingRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string[] HandledEventTypes =
    [
        nameof(AllocationCheckedOut),
        nameof(AllocationExpired),
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
                logger.LogError(ex, "Hostel waitlist re-ranking relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static Guid ExtractRoomId(string eventType, string payloadJson) => eventType switch
    {
        nameof(AllocationCheckedOut) => Deserialize<AllocationCheckedOut>(payloadJson).RoomId,
        nameof(AllocationExpired) => Deserialize<AllocationExpired>(payloadJson).RoomId,
        _ => throw new InvalidOperationException($"Unknown Hostel waitlist-re-ranking event type '{eventType}'."),
    };

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    private async Task ProcessPendingAsync(string eventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var service = scope.ServiceProvider.GetRequiredService<WaitlistReRankingService>();

        var messages = await outbox.GetUnprocessedAsync(eventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var roomId = ExtractRoomId(eventType, message.PayloadJson);
                var room = await rooms.GetByIdAsync(new RoomId(roomId), cancellationToken).ConfigureAwait(false);
                if (room is not null)
                {
                    await service.OfferToTopWaitlistedApplicantAsync(room.HostelId.Value, room.Type, $"system:waitlist-rerank:{message.Id}", cancellationToken).ConfigureAwait(false);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Hostel waitlist re-ranking relay: failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
