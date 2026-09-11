using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Events;
using UMS.Shared.Student;

namespace UMS.Workers.Career;

/// <summary>
/// CAR-17: fans Career's own domain events out to Notifications (requirement-spec.md §3 Consumers
/// columns, §7) - mirrors Alumni's own <c>AlumniNotificationRelayWorker</c> shape.
///
/// <para>
/// <b>Deliberately does NOT process <see cref="CareerApplicationCancelled"/></b> - CAR-9's/CAR-15's
/// withdrawal cascade handlers (`InternshipWithdrawalCascadeHandler`/`DriveCancellationCascadeHandler`)
/// already publish that notification synchronously, in-process, immediately after each cancellation
/// commits (requirement-spec.md §2.6's "mandatory NotificationRequest per Student"). Also relaying it
/// from the outbox here would double-notify the same Student for the same cancellation - the message
/// is left in the outbox unprocessed rather than acknowledged with no send, so its presence there is
/// itself the audit trail of "already handled synchronously," not a bug.
/// </para>
///
/// <para>
/// <b>Known, documented gap</b> - `InternshipPublished`'s "digest to eligible Students" (§3) has no
/// bounded recipient list to resolve: no per-Student subscription/opt-in list is built in this v1
/// (not named by any of the 18 tickets), the same posture Alumni's own
/// `AlumniNotificationRelayWorker` already documents for `JobPostingPublished`. "Drive reminders" (§3)
/// are similarly out of scope - no scheduled-time-before-drive reminder job exists in the
/// domain/application layer this worker drives.
/// </para>
/// </summary>
public sealed class CareerNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<CareerNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string InternshipPublishedEventType = typeof(InternshipPublished).Name;
    private static readonly string CareerApplicationStatusChangedEventType = typeof(CareerApplicationStatusChanged).Name;
    private static readonly string InterviewSlotBookedEventType = typeof(InterviewSlotBooked).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessInternshipPublishedAsync(stoppingToken).ConfigureAwait(false);
                await ProcessCareerApplicationStatusChangedAsync(stoppingToken).ConfigureAwait(false);
                await ProcessInterviewSlotBookedAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Career notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static Guid? ExtractGuid(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty(propertyName, out var element) && element.TryGetGuid(out var value) ? value : null;
    }

    /// <summary>Documented gap - see class remarks.</summary>
    private async Task ProcessInternshipPublishedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var messages = await outbox.GetUnprocessedAsync(InternshipPublishedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            logger.LogWarning(
                "Career notification relay: InternshipPublished's 'digest to eligible Students' has no bounded recipient list to resolve in this v1 (documented gap - see class remarks); skipping fan-out for outbox message {MessageId}.",
                message.Id);
            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Notifies the owning Student of any lifecycle transition (requirement-spec.md §3).</summary>
    private async Task ProcessCareerApplicationStatusChangedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var studentStatusChecker = scope.ServiceProvider.GetRequiredService<IStudentStatusChecker>();
        var publisher = scope.ServiceProvider.GetRequiredService<ICareerNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(CareerApplicationStatusChangedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var applicationId = ExtractGuid(message.PayloadJson, "CareerApplicationId") ?? throw new InvalidOperationException($"Empty {CareerApplicationStatusChangedEventType} payload.");
                var studentId = ExtractGuid(message.PayloadJson, "StudentId") ?? throw new InvalidOperationException($"Empty {CareerApplicationStatusChangedEventType} payload.");

                var standing = await studentStatusChecker.GetByStudentIdAsync(studentId, cancellationToken).ConfigureAwait(false);
                if (standing?.IdentityUserId is { } recipientId)
                {
                    await publisher.PublishAsync(new CareerNotificationRequest(CareerApplicationStatusChangedEventType, applicationId.ToString(), recipientId, new Dictionary<string, string> { ["careerApplicationId"] = applicationId.ToString() }), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("Career notification relay: CareerApplicationStatusChanged for CareerApplication {CareerApplicationId} has no resolvable Identity recipient - skipping.", applicationId);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Career notification relay: CareerApplicationStatusChanged publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }

    /// <summary>Booking confirmation to the Student who just claimed the slot (requirement-spec.md §3).</summary>
    private async Task ProcessInterviewSlotBookedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var studentStatusChecker = scope.ServiceProvider.GetRequiredService<IStudentStatusChecker>();
        var publisher = scope.ServiceProvider.GetRequiredService<ICareerNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(InterviewSlotBookedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var slotId = ExtractGuid(message.PayloadJson, "SlotId") ?? throw new InvalidOperationException($"Empty {InterviewSlotBookedEventType} payload.");
                var studentId = ExtractGuid(message.PayloadJson, "StudentId") ?? throw new InvalidOperationException($"Empty {InterviewSlotBookedEventType} payload.");

                var standing = await studentStatusChecker.GetByStudentIdAsync(studentId, cancellationToken).ConfigureAwait(false);
                if (standing?.IdentityUserId is { } recipientId)
                {
                    await publisher.PublishAsync(new CareerNotificationRequest(InterviewSlotBookedEventType, slotId.ToString(), recipientId, new Dictionary<string, string> { ["slotId"] = slotId.ToString() }), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("Career notification relay: InterviewSlotBooked for InterviewSlot {SlotId} has no resolvable Identity recipient - skipping confirmation.", slotId);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Career notification relay: InterviewSlotBooked publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
