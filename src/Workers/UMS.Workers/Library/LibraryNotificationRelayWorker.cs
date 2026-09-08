using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Events;

namespace UMS.Workers.Library;

/// <summary>
/// Fans Library's own outbox events out through <see cref="INotificationRequestPublisher"/>
/// (ADR-0009) - mirrors Hostel's/Admission's own NotificationRelayWorker exactly. Covers
/// requirement-spec.md §7/§3's named notices: due-date reminder is not built here (no ticket names
/// its concrete pre-due-date scheduling shape - a documented gap, the same posture Hostel's own
/// relay took for its "check-in reminder"), but every event-driven notice IS covered: loan issued/
/// renewed/returned, overdue, reservation created/fulfilled/expired, fine accrued/settled/waived.
/// </summary>
public sealed class LibraryNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string[] HandledEventTypes =
    [
        nameof(LoanIssued),
        nameof(LoanRenewed),
        nameof(LoanReturned),
        nameof(LoanOverdue),
        nameof(LoanLostWriteOff),
        nameof(ReservationCreated),
        nameof(ReservationFulfilled),
        nameof(ReservationExpired),
        nameof(FineAccrued),
        nameof(FineSettled),
        nameof(FineWaived),
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
                logger.LogError(ex, "Library notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static (Guid BorrowerId, string SourceEntityId, IReadOnlyDictionary<string, string?> MergeFields) Describe(string eventType, string payloadJson) => eventType switch
    {
        nameof(LoanIssued) => Map(Deserialize<LoanIssued>(payloadJson), e => (e.BorrowerId, e.LoanId.ToString()), e => new Dictionary<string, string?> { ["dueDate"] = e.DueDate.ToString("O") }),
        nameof(LoanRenewed) => Map(Deserialize<LoanRenewed>(payloadJson), e => (e.BorrowerId, e.LoanId.ToString()), e => new Dictionary<string, string?> { ["newDueDate"] = e.NewDueDate.ToString("O") }),
        nameof(LoanReturned) => Map(Deserialize<LoanReturned>(payloadJson), e => (e.BorrowerId, e.LoanId.ToString())),
        nameof(LoanOverdue) => Map(Deserialize<LoanOverdue>(payloadJson), e => (e.BorrowerId, e.LoanId.ToString()), e => new Dictionary<string, string?> { ["dueDate"] = e.DueDate.ToString("O") }),
        nameof(LoanLostWriteOff) => Map(Deserialize<LoanLostWriteOff>(payloadJson), e => (e.BorrowerId, e.LoanId.ToString())),
        nameof(ReservationCreated) => Map(Deserialize<ReservationCreated>(payloadJson), e => (e.BorrowerId, e.ReservationId.ToString())),
        nameof(ReservationFulfilled) => Map(Deserialize<ReservationFulfilled>(payloadJson), e => (e.BorrowerId, e.ReservationId.ToString()), e => new Dictionary<string, string?> { ["claimWindowExpiresAt"] = e.ClaimWindowExpiresAt.ToString("O") }),
        nameof(ReservationExpired) => Map(Deserialize<ReservationExpired>(payloadJson), e => (e.BorrowerId, e.ReservationId.ToString())),
        nameof(FineAccrued) => Map(Deserialize<FineAccrued>(payloadJson), e => (e.BorrowerId, e.FineId.ToString()), e => new Dictionary<string, string?> { ["totalAmount"] = e.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) }),
        nameof(FineSettled) => Map(Deserialize<FineSettled>(payloadJson), e => (e.BorrowerId, e.FineId.ToString()), e => new Dictionary<string, string?> { ["amount"] = e.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) }),
        nameof(FineWaived) => Map(Deserialize<FineWaived>(payloadJson), e => (e.BorrowerId, e.FineId.ToString()), e => new Dictionary<string, string?> { ["reason"] = e.Reason }),
        _ => throw new InvalidOperationException($"Unknown Library notification event type '{eventType}'."),
    };

    private static (Guid BorrowerId, string SourceEntityId, IReadOnlyDictionary<string, string?> MergeFields) Map<T>(T evt, Func<T, (Guid BorrowerId, string SourceEntityId)> select, Func<T, Dictionary<string, string?>>? mergeFields = null)
    {
        var (borrowerId, sourceEntityId) = select(evt);
        return (borrowerId, sourceEntityId, mergeFields?.Invoke(evt) ?? new Dictionary<string, string?>());
    }

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    private async Task ProcessPendingAsync(string eventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationRequestPublisher>();
        var borrowerContext = scope.ServiceProvider.GetRequiredService<BorrowerContextService>();

        var messages = await outbox.GetUnprocessedAsync(eventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var (borrowerId, sourceEntityId, mergeFields) = Describe(eventType, message.PayloadJson);
                var recipientUserId = await borrowerContext.ResolveRecipientUserIdAsync(borrowerId, cancellationToken).ConfigureAwait(false);
                if (recipientUserId is { } userId)
                {
                    await publisher.PublishAsync(new LibraryNotificationRequest(eventType, sourceEntityId, userId, mergeFields), cancellationToken).ConfigureAwait(false);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Library notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
