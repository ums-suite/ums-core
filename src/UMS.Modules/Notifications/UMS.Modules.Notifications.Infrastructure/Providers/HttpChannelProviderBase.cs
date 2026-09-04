using System.Net;
using System.Net.Http.Json;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>
/// Shared HTTP call shape for every external channel adapter (NTF-9 Email, NTF-10 SMS, NTF-11 Push,
/// and WhatsApp). Each channel's own subclass exists only to select its own named
/// <c>HttpClient</c>/<see cref="FakeProviderPrimaryHandler"/> pairing via DI (<see cref="DependencyInjection"/>)
/// and to report its own <see cref="IChannelProvider.Channel"/> - the actual request/response
/// handling and Polly-wrapped resilience posture (via <c>UMS.Shared.Resilience.AddUmsResilientHttpClient</c>)
/// is identical across all four, matching a real gateway's typical send-and-acknowledge shape.
/// </summary>
internal abstract class HttpChannelProviderBase(HttpClient httpClient) : IChannelProvider
{
    public abstract NotificationChannel Channel { get; }

    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/send",
                new FakeGatewaySendRequest(request.Destination, request.Subject, request.Body, request.DeepLink, request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var payload = await response.Content.ReadFromJsonAsync<FakeGatewaySendResponse>(cancellationToken).ConfigureAwait(false);
                return ChannelSendResult.Success(payload?.ProviderMessageId ?? request.IdempotencyKey);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests or HttpStatusCode.GatewayTimeout
                ? ChannelSendResult.TransientFailure($"Fake {Channel} gateway returned {(int)response.StatusCode}: {body}")
                : ChannelSendResult.PermanentFailure($"Fake {Channel} gateway rejected the send with {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // UMS.Shared.Resilience's standard handler already retried/circuit-broke around this
            // call before it ever surfaces here - reaching this catch means the whole resilience
            // pipeline gave up, which is itself a transient (not permanent) outcome from
            // NotificationDeliveryAttempt's own point of view (design-decisions.md "Retry/Backoff
            // Design Per Channel").
            return ChannelSendResult.TransientFailure($"{Channel} provider call failed after the resilience pipeline's own retries: {ex.Message}");
        }
    }

    private sealed record FakeGatewaySendRequest(string Destination, string? Subject, string Body, string? DeepLink, string IdempotencyKey);

    private sealed record FakeGatewaySendResponse(string Status, string ProviderMessageId);
}
