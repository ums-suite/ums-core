using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Application.Abstractions;

/// <summary>
/// requirement-spec.md §4 invariant: "Notifications is the only module holding
/// IEmailProvider/ISmsProvider/push credentials" (ADR-0009). One implementation per external
/// channel (Email/SMS/Push/WhatsApp - not InApp, which Notifications owns end-to-end per §4's
/// "must succeed" invariant and needs no external provider). Every real implementation is an
/// <c>HttpClientFactory</c> typed client wrapped in Polly via <c>UMS.Shared.Resilience</c>
/// (ums-conventions.md, Resilience &amp; Reliability).
///
/// <para>
/// <b>Not a real provider integration</b> - see each Infrastructure implementation's own remarks:
/// this build has no real Twilio/SendGrid/Meta WhatsApp Business API/FCM credentials or sandbox
/// account available in this environment, so every implementation talks to an in-process fake
/// terminal <see cref="HttpMessageHandler"/> that simulates a real gateway's latency and occasional
/// failure while still exercising the genuine <c>HttpClientFactory</c> + Polly retry/circuit-breaker
/// pipeline for real.
/// </para>
/// </summary>
public interface IChannelProvider
{
    public NotificationChannel Channel { get; }

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>One outbound send, channel-agnostic - <see cref="Destination"/> is an email address, E.164 phone number, or push token depending on the target <see cref="IChannelProvider.Channel"/>.</summary>
/// <param name="IdempotencyKey">Passed through to the (fake) provider as a client-supplied idempotency key where the channel supports it - design-decisions.md "Retry/Backoff Design Per Channel": "provider-side idempotency keys where the channel adapter supports them" as a second layer of defense against a duplicate send.</param>
public sealed record ChannelSendRequest(string Destination, string? Subject, string Body, string? DeepLink, string IdempotencyKey);

public sealed record ChannelSendResult(bool IsSuccess, string? ProviderMessageId, string? Error, bool IsTransient)
{
    public static ChannelSendResult Success(string providerMessageId) => new(true, providerMessageId, null, false);

    public static ChannelSendResult TransientFailure(string error) => new(false, null, error, true);

    public static ChannelSendResult PermanentFailure(string error) => new(false, null, error, false);
}
