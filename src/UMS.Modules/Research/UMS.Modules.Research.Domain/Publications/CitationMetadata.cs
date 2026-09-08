using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Domain.Publications;

/// <summary>
/// requirement-spec.md §3: <c>(doi: nullable, publicationDate, citationCount: nullable)</c>.
/// <see cref="NormalizedDoi"/> is what the database unique constraint (§4: "globally unique within
/// Research's schema, case-insensitive, normalized") is actually built on - see
/// <c>PublicationConfiguration</c>'s own remarks.
///
/// <para>Open question carried forward, not resolved here (requirement-spec.md §9): <see
/// cref="CitationCount"/> is a static, manually-entered field only - no external citation-index
/// integration (Crossref, Google Scholar) exists in this pass.</para>
/// </summary>
public sealed record CitationMetadata
{
    private CitationMetadata(string? doi, DateOnly publicationDate, int? citationCount)
    {
        Doi = doi;
        PublicationDate = publicationDate;
        CitationCount = citationCount;
    }

    public string? Doi { get; }

    /// <summary>Trimmed, lower-cased form of <see cref="Doi"/> - <see langword="null"/> when <see cref="Doi"/> is absent. The DOI-uniqueness invariant (§4) is enforced against this value, not the raw <see cref="Doi"/>.</summary>
    public string? NormalizedDoi => Doi?.Trim().ToLowerInvariant();

    public DateOnly PublicationDate { get; }

    public int? CitationCount { get; }

    public static Result<CitationMetadata> Create(string? doi, DateOnly publicationDate, int? citationCount)
    {
        if (citationCount is < 0)
        {
            return Error.Validation("citationmetadata.negative_citation_count", "Citation count must not be negative.");
        }

        return new CitationMetadata(string.IsNullOrWhiteSpace(doi) ? null : doi.Trim(), publicationDate, citationCount);
    }

    /// <summary>For infrastructure round-tripping of an already-validated stored value only.</summary>
    public static CitationMetadata FromStoredValue(string? doi, DateOnly publicationDate, int? citationCount) => new(doi, publicationDate, citationCount);
}
