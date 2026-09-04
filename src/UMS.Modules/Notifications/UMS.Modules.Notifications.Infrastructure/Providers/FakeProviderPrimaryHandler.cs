using System.Net;
using Microsoft.Extensions.Options;

namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>
/// <b>Not a real provider integration.</b> This environment has no real Twilio/SendGrid/Meta
/// WhatsApp Business API/FCM credentials or sandbox account available, so every channel provider
/// (NTF-9/10/11, plus WhatsApp) talks to this in-process fake terminal <see cref="HttpMessageHandler"/>
/// instead of a real socket - registered as each typed client's <c>PrimaryHttpMessageHandler</c>
/// (<see cref="DependencyInjection"/>), with <c>UMS.Shared.Resilience</c>'s real
/// <c>AddStandardResilienceHandler()</c> (retry with exponential backoff+jitter, circuit breaker,
/// timeout) wrapped around it exactly as it would be around a real provider's socket handler - so
/// the resilience pipeline itself is genuine, only the "provider" underneath it is simulated.
/// Simulates realistic latency and an injectable failure rate (or a forced total outage, for
/// manually exercising edge-cases.md's "SMS provider outage -&gt; dead-letter after bounded
/// retries" case) and returns a response shaped like a real gateway's own send acknowledgment.
/// </summary>
internal sealed class FakeProviderPrimaryHandler(string channelName, IOptionsMonitor<FakeProviderGatewayOptions> optionsMonitor) : HttpMessageHandler
{
    private static readonly Random _random = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(channelName);

        var latencyMs = _random.Next(options.MinLatencyMs, Math.Max(options.MinLatencyMs, options.MaxLatencyMs) + 1);
        await Task.Delay(latencyMs, cancellationToken).ConfigureAwait(false);

        if (options.ForceOutage || _random.NextDouble() < options.FailureRate)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = request,
                Content = new StringContent("""{"error":"simulated_gateway_unavailable"}"""),
            };
        }

        var providerMessageId = $"fake-{channelName.ToLowerInvariant()}-{Guid.NewGuid():N}";
        return new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            RequestMessage = request,
            Content = new StringContent($$"""{"status":"queued","providerMessageId":"{{providerMessageId}}"}"""),
        };
    }
}
