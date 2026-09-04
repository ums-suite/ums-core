using Microsoft.Extensions.Logging;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Application.Webhooks;

/// <summary>
/// NTF-15: <c>POST /notifications/webhooks/email</c>, <c>POST /notifications/webhooks/sms</c> -
/// provider bounce/delivery-receipt callbacks. §8 edge case: "Hard email bounce -&gt; the
/// recipient's email is marked suppressed for future automated sends".
///
/// <para>
/// <b>Simplification, documented:</b> this build's fake channel providers (see each Infrastructure
/// <c>IChannelProvider</c> implementation's own remarks) respond synchronously and do not themselves
/// call back into these webhook endpoints asynchronously the way a real Twilio/SendGrid gateway
/// would. These endpoints are still fully real and independently exercised - integration tests and
/// manual verification POST synthetic payloads shaped exactly like a real provider's bounce/delivery
/// callback directly at them - just not wired to be triggered automatically by the fake gateway's
/// own send responses.
/// </para>
/// </summary>
public sealed class ProviderWebhookService(
    INotificationDeliveryAttemptRepository attempts,
    IChannelSuppressionRepository suppressions,
    INotificationRequestRepository requests,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ProviderWebhookService> logger)
{
    public async Task HandleAsync(NotificationChannel channel, ProviderWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetByProviderMessageIdAsync(channel, payload.ProviderMessageId, cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            logger.LogWarning("Webhook callback for unknown {Channel} provider message id {ProviderMessageId}.", channel, payload.ProviderMessageId);
            return;
        }

        if (payload.Event != ProviderWebhookEvent.HardBounce)
        {
            // Delivered/soft-bounce receipts are informational only in this build - the attempt is
            // already Delivered by the time a real provider's async receipt would arrive (this
            // build's fake providers respond synchronously - see this class's own remarks).
            return;
        }

        var request = await requests.GetByIdAsync(attempt.NotificationRequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return;
        }

        if (await suppressions.IsSuppressedAsync(request.RecipientId, channel, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        suppressions.Add(ChannelSuppression.Create(request.RecipientId, channel, $"Hard bounce ({payload.Reason ?? "no reason given"}) reported {clock.UtcNow:O}.", clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
