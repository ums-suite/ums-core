namespace UMS.Modules.Learning.Application.Abstractions;

/// <summary>
/// LRN-8: the external similarity-check provider, behind this module's own port so the Application
/// layer never sees an <c>HttpClient</c>.
///
/// <para>
/// <b>Every call is Polly-wrapped</b> (retry with exponential backoff + jitter, circuit breaker,
/// timeout) via <c>UMS.Shared.Resilience</c>'s <c>AddUmsResilientHttpClient</c> - never a bare HTTP
/// call (ums-conventions.md, Resilience &amp; Reliability: "never hand-rolled per integration";
/// edge-cases.md's "The plagiarism-check provider is slow or down" is this integration's required
/// per-integration documentation).
/// </para>
///
/// <para>
/// <b>Not a real provider integration.</b> This environment has no real Turnitin/iThenticate/Copyleaks
/// credentials or sandbox account, so the one implementation talks to an in-process fake terminal
/// <c>HttpMessageHandler</c> - the identical posture Notifications took for Email/SMS/WhatsApp/Push
/// (see <c>FakePlagiarismProviderHandler</c>'s own remarks). The resilience pipeline itself is
/// genuine; only the "provider" underneath it is simulated.
/// </para>
/// </summary>
public interface IPlagiarismCheckProvider
{
    public string ProviderName { get; }

    public Task<PlagiarismProviderResult> CheckAsync(PlagiarismProviderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>One submission's content, as handed to the provider. <paramref name="IdempotencyKey"/> is the check's own id, so a retried call against a provider that supports client-supplied keys never double-charges quota.</summary>
public sealed record PlagiarismProviderRequest(Guid SubmissionId, string? TextContent, IReadOnlyCollection<Guid> ArtifactIds, string IdempotencyKey);

/// <summary>
/// The provider's verdict. A failure here is a real, terminal, Instructor-visible outcome - the
/// caller transitions the check to <c>Failed</c>, never to a fabricated "0% similarity"
/// (requirement-spec.md learning §2: "never silently treated as clean").
/// </summary>
public sealed record PlagiarismProviderResult(bool IsSuccess, decimal SimilarityPercentage, string MatchedSourceSummary, string? Error)
{
    public static PlagiarismProviderResult Success(decimal similarityPercentage, string matchedSourceSummary) =>
        new(true, similarityPercentage, matchedSourceSummary, null);

    public static PlagiarismProviderResult Failure(string error) => new(false, 0m, string.Empty, error);
}
