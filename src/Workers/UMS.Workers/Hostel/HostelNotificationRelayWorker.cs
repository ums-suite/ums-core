using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Events;
using UMS.Shared.Student;

namespace UMS.Workers.Hostel;

/// <summary>
/// Fans Hostel's own outbox events out through <see cref="INotificationRequestPublisher"/> (ADR-0009)
/// - mirrors Admission's/Finance's own NotificationRelayWorker exactly. Covers requirement-spec.md
/// §7/§3's named notices: application submitted, allocation approved/bed allocated (folding "fee
/// due" into the same BedAllocated notice, since HOS-8's invoice creation happens in the same
/// transaction), and complaint resolved. A true pre-deadline "check-in reminder" (a scheduled nudge
/// before the grace deadline, distinct from a post-check-in confirmation) is a documented gap - not
/// built in this pass, since no ticket names its concrete scheduling shape.
/// </summary>
public sealed class HostelNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<HostelNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string[] HandledEventTypes =
    [
        nameof(HostelApplicationSubmitted),
        nameof(HostelApplicationApproved),
        nameof(HostelApplicationRejected),
        nameof(HostelApplicationWithdrawn),
        nameof(BedAllocated),
        nameof(AllocationActivated),
        nameof(AllocationCheckedOut),
        nameof(AllocationExpired),
        nameof(ComplaintResolved),
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
                logger.LogError(ex, "Hostel notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static (Guid StudentId, string SourceEntityId, IReadOnlyDictionary<string, string?> MergeFields) Describe(string eventType, string payloadJson) => eventType switch
    {
        nameof(HostelApplicationSubmitted) => Map(Deserialize<HostelApplicationSubmitted>(payloadJson), e => (e.StudentId, e.ApplicationId.ToString())),
        nameof(HostelApplicationApproved) => Map(Deserialize<HostelApplicationApproved>(payloadJson), e => (e.StudentId, e.ApplicationId.ToString())),
        nameof(HostelApplicationRejected) => Map(Deserialize<HostelApplicationRejected>(payloadJson), e => (e.StudentId, e.ApplicationId.ToString()), e => new Dictionary<string, string?> { ["reason"] = e.Reason }),
        nameof(HostelApplicationWithdrawn) => Map(Deserialize<HostelApplicationWithdrawn>(payloadJson), e => (e.StudentId, e.ApplicationId.ToString())),
        nameof(BedAllocated) => Map(Deserialize<BedAllocated>(payloadJson), e => (e.StudentId, e.AllocationId.ToString())),
        nameof(AllocationActivated) => Map(Deserialize<AllocationActivated>(payloadJson), e => (e.StudentId, e.AllocationId.ToString())),
        nameof(AllocationCheckedOut) => Map(Deserialize<AllocationCheckedOut>(payloadJson), e => (e.StudentId, e.AllocationId.ToString()), e => new Dictionary<string, string?> { ["checkOutType"] = e.CheckOutType, ["refundEligible"] = e.RefundEligible.ToString() }),
        nameof(AllocationExpired) => Map(Deserialize<AllocationExpired>(payloadJson), e => (e.StudentId, e.AllocationId.ToString())),
        nameof(ComplaintResolved) => Map(Deserialize<ComplaintResolved>(payloadJson), e => (e.StudentId, e.ComplaintId.ToString()), e => new Dictionary<string, string?> { ["resolution"] = e.Resolution }),
        _ => throw new InvalidOperationException($"Unknown Hostel notification event type '{eventType}'."),
    };

    private static (Guid StudentId, string SourceEntityId, IReadOnlyDictionary<string, string?> MergeFields) Map<T>(T evt, Func<T, (Guid StudentId, string SourceEntityId)> select, Func<T, Dictionary<string, string?>>? mergeFields = null)
    {
        var (studentId, sourceEntityId) = select(evt);
        return (studentId, sourceEntityId, mergeFields?.Invoke(evt) ?? new Dictionary<string, string?>());
    }

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    private async Task ProcessPendingAsync(string eventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationRequestPublisher>();
        var studentStatusChecker = scope.ServiceProvider.GetRequiredService<IStudentStatusChecker>();

        var messages = await outbox.GetUnprocessedAsync(eventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var (studentId, sourceEntityId, mergeFields) = Describe(eventType, message.PayloadJson);
                var standing = await studentStatusChecker.GetByStudentIdAsync(studentId, cancellationToken).ConfigureAwait(false);
                if (standing?.IdentityUserId is { } recipientUserId)
                {
                    await publisher.PublishAsync(new HostelNotificationRequest(eventType, sourceEntityId, recipientUserId, mergeFields), cancellationToken).ConfigureAwait(false);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Hostel notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
