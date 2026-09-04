using Microsoft.Extensions.Logging;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.Application.Requests;

/// <summary>
/// The one real implementation of <see cref="INotificationRequestIntake"/> (NTF-3/NTF-4/NTF-6, and
/// NTF-5's enqueue-time fast-fail optimization - design-decisions.md "Opt-Out-Check Timing": "An
/// enqueue-time check may still run as a fast-fail optimization ... but it is never treated as
/// sufficient on its own" - the authoritative opt-out gate is the send-time check in
/// <see cref="Dispatch.NotificationDispatchService"/>).
/// </summary>
public sealed class SubmitNotificationRequestService(
    INotificationRequestRepository requests,
    IRecipientPreferenceRepository preferences,
    IOtpRateLimiter otpRateLimiter,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<SubmitNotificationRequestService> logger) : INotificationRequestIntake
{
    public async Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default)
    {
        var defaults = NotificationEventCatalog.Resolve(request.EventType);

        // NTF-5 fast-fail: skip fan-out entirely for an obviously, already-opted-out recipient
        // rather than creating a NotificationRequest (and its per-channel attempts) that would only
        // be suppressed at send time anyway. Never authoritative on its own (see this method's own
        // remarks) - the real gate is send-time, per-channel, in the dispatch pipeline.
        if (!defaults.Category.IsMandatory() && await preferences.IsOptedOutAsync(request.RecipientId, defaults.Category, cancellationToken).ConfigureAwait(false))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Notification request for {EventType}/{RecipientId} skipped at enqueue time - recipient opted out of category {Category}.",
                    request.EventType,
                    request.RecipientId,
                    defaults.Category);
            }

            return Guid.Empty;
        }

        // requirement-spec.md §5 NFR Rate Limiting (NTF-10) - checked here, at acceptance, rather
        // than at send time: an OTP that will never be dispatched at all (because the recipient is
        // already over their window) should never even occupy a NotificationRequest/attempt row.
        if (defaults.Category == NotificationCategory.Otp && !await otpRateLimiter.TryAcquireAsync(request.RecipientId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Guid>(Error.Validation("notification_request.otp_rate_limited", "This recipient has exceeded the OTP request rate limit - try again shortly."));
        }

        var createResult = NotificationRequest.Create(
            request.SourceModule,
            request.EventType,
            request.SourceEntityId,
            request.RecipientId,
            defaults.Category,
            defaults.Priority,
            defaults.Channels,
            request.PayloadJson,
            request.LanguageOverride,
            clock.UtcNow);

        if (createResult.IsFailure)
        {
            return Result.Failure<Guid>(createResult.Error!);
        }

        var notificationRequest = createResult.Value;
        requests.Add(notificationRequest);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateNotificationRequestException)
        {
            // design-decisions.md, "Dedup-Key Enforcement Mechanism": "a constraint violation is
            // treated as 'already accepted' and returned as success to the calling outbox relay,
            // never surfaced as an error requiring the relay to retry further."
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Notification request for {SourceModule}/{EventType}/{SourceEntityId}/{RecipientId} was already accepted (dedupe constraint) - treated as success.",
                    request.SourceModule,
                    request.EventType,
                    request.SourceEntityId,
                    request.RecipientId);
            }

            return Guid.Empty;
        }

        return notificationRequest.Id.Value;
    }
}
