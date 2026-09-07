using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using UMS.Modules.Learning.Application.Abstractions;

namespace UMS.Modules.Learning.Infrastructure.Plagiarism;

/// <summary>
/// LRN-8's one external integration. A typed <see cref="HttpClient"/> registered through
/// <c>UMS.Shared.Resilience.AddUmsResilientHttpClient</c>, so retry-with-backoff+jitter, a circuit
/// breaker, and a per-call timeout all sit between this method and the (fake, see
/// <see cref="FakePlagiarismProviderHandler"/>) provider - never a bare HTTP call
/// (ums-conventions.md, Resilience &amp; Reliability).
///
/// <para>
/// Reaching the catch below means the WHOLE resilience pipeline gave up, which is exactly the
/// signal edge-cases.md's "The plagiarism-check provider is slow or down" decision wants surfaced
/// as a real <c>Failed</c> terminal status - never as a fabricated clean result.
/// </para>
/// </summary>
internal sealed class HttpPlagiarismCheckProvider(HttpClient httpClient, IOptionsMonitor<FakePlagiarismGatewayOptions> optionsMonitor) : IPlagiarismCheckProvider
{
    public string ProviderName => optionsMonitor.CurrentValue.ProviderName;

    public async Task<PlagiarismProviderResult> CheckAsync(PlagiarismProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/similarity-checks",
                new GatewayCheckRequest(request.SubmissionId, request.TextContent, request.ArtifactIds, request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return PlagiarismProviderResult.Failure($"The similarity-check provider returned {(int)response.StatusCode}: {errorBody}");
            }

            var payload = await response.Content.ReadFromJsonAsync<GatewayCheckResponse>(cancellationToken).ConfigureAwait(false);
            return payload is null
                ? PlagiarismProviderResult.Failure("The similarity-check provider returned an empty response body.")
                : PlagiarismProviderResult.Success(payload.SimilarityPercentage, payload.MatchedSourceSummary);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return PlagiarismProviderResult.Failure($"The similarity-check provider call failed after the resilience pipeline's own retries: {ex.Message}");
        }
    }

    private sealed record GatewayCheckRequest(Guid SubmissionId, string? TextContent, IReadOnlyCollection<Guid> ArtifactIds, string IdempotencyKey);

    private sealed record GatewayCheckResponse(decimal SimilarityPercentage, string MatchedSourceSummary);
}
