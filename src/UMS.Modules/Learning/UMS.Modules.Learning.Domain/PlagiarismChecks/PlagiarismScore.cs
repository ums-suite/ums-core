using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.PlagiarismChecks;

/// <summary>
/// requirement-spec.md learning §3: percentage similarity + matched-source summary + provider name,
/// owned by <see cref="PlagiarismCheck"/>. Reviewable evidence for the Instructor, never a
/// pass/fail verdict the platform acts on itself (ADR-0018's "flag, don't auto-decide").
/// </summary>
public sealed record PlagiarismScore
{
    private PlagiarismScore(decimal similarityPercentage, string matchedSourceSummary, string providerName)
    {
        SimilarityPercentage = similarityPercentage;
        MatchedSourceSummary = matchedSourceSummary;
        ProviderName = providerName;
    }

    /// <summary>0-100.</summary>
    public decimal SimilarityPercentage { get; }

    public string MatchedSourceSummary { get; }

    public string ProviderName { get; }

    public static Result<PlagiarismScore> Create(decimal similarityPercentage, string matchedSourceSummary, string providerName)
    {
        if (similarityPercentage is < 0m or > 100m)
        {
            return Error.Validation("plagiarism_score.out_of_range", "A PlagiarismScore's similarity percentage must be between 0 and 100.");
        }

        return string.IsNullOrWhiteSpace(providerName)
            ? Error.Validation("plagiarism_score.provider_required", "A PlagiarismScore must name the provider that produced it.")
            : new PlagiarismScore(similarityPercentage, (matchedSourceSummary ?? string.Empty).Trim(), providerName.Trim());
    }
}
