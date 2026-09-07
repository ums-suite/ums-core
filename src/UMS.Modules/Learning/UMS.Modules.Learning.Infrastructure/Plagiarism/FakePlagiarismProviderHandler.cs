using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace UMS.Modules.Learning.Infrastructure.Plagiarism;

/// <summary>
/// <b>Not a real provider integration.</b> This environment has no Turnitin/iThenticate/Copyleaks
/// credentials or sandbox account available, so LRN-8's provider call talks to this in-process fake
/// terminal <see cref="HttpMessageHandler"/> instead of a real socket - registered as the typed
/// client's <c>PrimaryHttpMessageHandler</c> (see <c>DependencyInjection</c>), with
/// <c>UMS.Shared.Resilience</c>'s real <c>AddStandardResilienceHandler()</c> (retry with
/// exponential backoff + jitter, circuit breaker, timeout) wrapped around it exactly as it would be
/// around a real provider's socket handler. The resilience pipeline is therefore genuine; only the
/// "provider" underneath it is simulated. This is the identical posture Notifications took for
/// Email/SMS/WhatsApp/Push (<c>FakeProviderPrimaryHandler</c>) rather than a Learning-specific
/// shortcut.
/// </summary>
/// <remarks>
/// The similarity figure it returns is a deterministic function of the submitted content's own
/// hash, not a random number: the same content always scores the same, so an integration test can
/// assert on a specific value and a manual pass sees stable, explicable results instead of noise.
/// </remarks>
internal sealed class FakePlagiarismProviderHandler(IOptionsMonitor<FakePlagiarismGatewayOptions> optionsMonitor) : HttpMessageHandler
{
    private static readonly Random Random = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = optionsMonitor.CurrentValue;

        var latencyMs = Random.Next(options.MinLatencyMs, Math.Max(options.MinLatencyMs, options.MaxLatencyMs) + 1);
        await Task.Delay(latencyMs, cancellationToken).ConfigureAwait(false);

        if (options.ForceOutage || Random.NextDouble() < options.FailureRate)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = request,
                Content = new StringContent("""{"error":"simulated_similarity_gateway_unavailable"}"""),
            };
        }

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var similarity = DeterministicSimilarity(body);
        var matched = similarity > 0m
            ? $"{(similarity > 40m ? 2 : 1)} matched source(s) in the simulated corpus"
            : "No matched sources in the simulated corpus";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(
                $$"""{"similarityPercentage":{{similarity.ToString(CultureInfo.InvariantCulture)}},"matchedSourceSummary":"{{matched}}"}"""),
        };
    }

    private static decimal DeterministicSimilarity(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return decimal.Round((hash[0] * 100m) / 255m, 2, MidpointRounding.AwayFromZero);
    }
}
