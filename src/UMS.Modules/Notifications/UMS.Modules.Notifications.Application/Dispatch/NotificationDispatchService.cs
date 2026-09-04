using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Events;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Shared.Audit;
using UMS.Shared.Identity;

namespace UMS.Modules.Notifications.Application.Dispatch;

/// <summary>
/// The per-attempt processing pipeline every channel's dispatch worker (NTF-13) calls once it has
/// claimed a batch of eligible attempts (<see cref="INotificationDeliveryAttemptRepository.ClaimBatchAsync"/>
/// already transitioned each claimed row to <c>InFlight</c> - see that method's own remarks on why
/// claiming and processing are two separate steps). Implements, in one place: NTF-6's per-channel
/// independence (a failure processing one attempt never touches another), NTF-5's send-time
/// authoritative opt-out gate, NTF-9/10/11/12's channel dispatch (InApp handled specially per §4's
/// "must succeed" invariant), NTF-13's retry/dead-letter transitions, and NTF-17's Audit integration.
/// </summary>
public sealed class NotificationDispatchService(
    INotificationRequestRepository requests,
    INotificationDeliveryAttemptRepository attempts,
    ITemplateRepository templates,
    IRecipientPreferenceRepository preferences,
    IChannelSuppressionRepository suppressions,
    IRecipientDirectory recipientDirectory,
    IReadOnlyDictionary<NotificationChannel, IChannelProvider> providers,
    IAuditRecorder auditRecorder,
    IDomainEventRecorder domainEventRecorder,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<NotificationDispatchService> logger)
{
    public async Task ProcessClaimedAsync(ClaimedAttempt claimed, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var attempt = await attempts.GetByIdAsync(new NotificationDeliveryAttemptId(claimed.AttemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            logger.LogWarning("Claimed delivery attempt {AttemptId} no longer exists - skipping.", claimed.AttemptId);
            return;
        }

        var request = await requests.GetByIdAsync(new NotificationRequestId(claimed.NotificationRequestId), cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            logger.LogError("Delivery attempt {AttemptId} references NotificationRequest {RequestId} which no longer exists.", claimed.AttemptId, claimed.NotificationRequestId);
            return;
        }

        // design-decisions.md, "Opt-Out-Check Timing": the authoritative, send-time gate - applies
        // uniformly across all channels including InApp (edge-cases.md's own "Resolved" bullet).
        if (!request.Category.IsMandatory() && await preferences.IsOptedOutAsync(request.RecipientId, request.Category, cancellationToken).ConfigureAwait(false))
        {
            attempt.MarkSuppressedByOptOut(now);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (attempt.Channel != NotificationChannel.InApp && await suppressions.IsSuppressedAsync(request.RecipientId, attempt.Channel, cancellationToken).ConfigureAwait(false))
        {
            attempt.MarkPermanentFailure("Recipient's contact info on this channel is suppressed (prior hard bounce/opt-out) - see NTF-15.", now);
            await FinalizeAsync(attempt, request, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        var contactInfo = await recipientDirectory.GetContactInfoAsync(request.RecipientId, cancellationToken).ConfigureAwait(false);
        var destination = ResolveDestination(attempt.Channel, contactInfo);

        // §8 edge case "Recipient has no verified email/phone on file" - InApp never needs a
        // destination (Notifications owns it end-to-end), so it is exempt from this gate.
        if (attempt.Channel != NotificationChannel.InApp && destination is null)
        {
            attempt.MarkNoContactInfo(now);
            await FinalizeAsync(attempt, request, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        var languageCode = request.LanguageOverride ?? contactInfo?.PreferredLanguageCode ?? Domain.Templates.Template.EnglishLanguageCode;
        var template = await templates.GetByEventTypeAndChannelAsync(request.EventType, attempt.Channel, cancellationToken).ConfigureAwait(false);
        var translation = template?.Resolve(languageCode);

        if (translation is null)
        {
            attempt.MarkTemplateMissing(now);
            await FinalizeAsync(attempt, request, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        var renderedBody = TemplateRenderer.Render(translation.Body, request.PayloadJson);
        var renderedSubject = translation.Subject is null ? null : TemplateRenderer.Render(translation.Subject, request.PayloadJson);
        var renderedDeepLink = translation.DeepLink;

        if (attempt.Channel == NotificationChannel.InApp)
        {
            // §4 invariant: "In-app is treated as Notifications' own 'must succeed' channel" - no
            // external provider, so this always succeeds once it reaches this point.
            attempt.MarkDelivered(now, providerMessageId: null, renderedSubject, renderedBody, renderedDeepLink);
            await FinalizeAsync(attempt, request, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!providers.TryGetValue(attempt.Channel, out var provider))
        {
            logger.LogError("No IChannelProvider registered for channel {Channel}.", attempt.Channel);
            return;
        }

        var sendResult = await provider.SendAsync(
            new ChannelSendRequest(destination!, renderedSubject, renderedBody, renderedDeepLink, IdempotencyKey: attempt.Id.ToString()),
            cancellationToken).ConfigureAwait(false);

        if (sendResult.IsSuccess)
        {
            attempt.MarkDelivered(now, sendResult.ProviderMessageId, renderedSubject, renderedBody, renderedDeepLink);
        }
        else if (sendResult.IsTransient)
        {
            attempt.MarkFailedTransient(sendResult.Error ?? "Unknown transient provider failure.", now);
        }
        else
        {
            attempt.MarkPermanentFailure(sendResult.Error ?? "Provider rejected the send.", now);
        }

        await FinalizeAsync(attempt, request, now, cancellationToken).ConfigureAwait(false);
    }

    private static string? ResolveDestination(NotificationChannel channel, RecipientContactInfo? contactInfo) => channel switch
    {
        NotificationChannel.Email => contactInfo?.Email?.Value,
        NotificationChannel.Sms or NotificationChannel.WhatsApp => contactInfo?.Mobile?.Value,
        NotificationChannel.Push => contactInfo?.PushToken,
        NotificationChannel.InApp => "in-app",
        _ => null,
    };

    /// <summary>
    /// Persists the attempt's new state and, for a terminal outcome (Delivered/DeadLettered) on an
    /// audit-required category (NTF-17), writes the matching <c>AuditLogEntry</c> in the SAME
    /// transaction (ADR-0012) - mirrors Identity's own <c>UserStatusService</c> call-site pattern
    /// exactly. A <see cref="Retrying"/> outcome is persisted plainly, with no audit write (audit
    /// fires once, on the attempt's actual terminal outcome, not on every intermediate retry).
    /// </summary>
    private async Task FinalizeAsync(NotificationDeliveryAttempt attempt, NotificationRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Domain.Common.IDomainEvent? domainEvent = attempt.Status switch
        {
            DeliveryAttemptStatus.Delivered => new NotificationDeliveryAttemptSucceeded(request.Id.Value, attempt.Id.Value, attempt.Channel, request.RecipientId, request.Category, now),
            DeliveryAttemptStatus.DeadLettered => new NotificationDeadLettered(request.Id.Value, attempt.Id.Value, attempt.Channel, request.Category, attempt.DeadLetterReason!.Value, request.RecipientId, now),
            DeliveryAttemptStatus.Retrying => new NotificationDeliveryAttemptFailed(request.Id.Value, attempt.Id.Value, attempt.Channel, attempt.LastError ?? "unknown", attempt.AttemptCount, now),
            _ => null,
        };

        var requiresAudit = request.Category.IsAuditRequired()
            && attempt.Status is DeliveryAttemptStatus.Delivered or DeliveryAttemptStatus.DeadLettered;

        if (!requiresAudit)
        {
            if (domainEvent is not null)
            {
                domainEventRecorder.Enqueue(domainEvent);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvent is not null)
        {
            domainEventRecorder.Enqueue(domainEvent);
        }

        var auditRequest = new RecordAuditEntryRequest(
            ActorId: "system:notifications-dispatch",
            ActorType: AuditActorType.System,
            IpAddress: null,
            Application: "system",
            EntityType: "NotificationDeliveryAttempt",
            EntityId: attempt.Id.ToString(),
            Action: attempt.Status.ToString().ToLowerInvariant(),
            BeforeValueJson: null,
            AfterValueJson: JsonSerializer.Serialize(new { channel = attempt.Channel.ToString(), status = attempt.Status.ToString(), category = request.Category.ToString(), reason = attempt.DeadLetterReason?.ToString() }),
            CorrelationId: request.Id.Value.ToString());

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError("Failed to record NTF-17 audit entry for delivery attempt {AttemptId}: {Error}", attempt.Id, auditResult.Error);
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
