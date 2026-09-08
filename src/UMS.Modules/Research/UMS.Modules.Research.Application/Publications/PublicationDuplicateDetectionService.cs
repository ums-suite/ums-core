using System.Text;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.Application.Publications;

/// <summary>
/// RES-9: design-decisions.md "Publication Duplicate Detection and Merge Mechanism" - the
/// DOI-absent half of the layered detection (the DOI-bearing half is a hard database unique
/// constraint, enforced at the persistence layer, not here). Flags a <see
/// cref="PublicationDuplicateCandidate"/> for Admin review; never blocks the write, never merges
/// automatically (edge-cases.md).
///
/// <para>
/// Residual note carried forward, not resolved here (edge-cases.md's own text): "the concrete
/// fuzzy-match threshold/algorithm is left to the implementation pass." This is a deliberately
/// simple first pass - normalized-equality on (title, venue name, publication year), not a genuine
/// fuzzy/Levenshtein-distance match - flagged explicitly rather than silently assumed to be more
/// sophisticated than it is.
/// </para>
/// </summary>
public sealed class PublicationDuplicateDetectionService(IPublicationRepository publications, IPublicationDuplicateCandidateRepository candidates, IClock clock)
{
    public async Task<PublicationDuplicateCandidate?> DetectAsync(Publication newPublication, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(newPublication.Citation.Doi))
        {
            // The DOI-bearing case is fully handled by the database's own hard uniqueness
            // constraint (§4) - never needs a PublicationDuplicateCandidate.
            return null;
        }

        var year = newPublication.Citation.PublicationDate.Year;
        var normalizedTitle = Normalize(newPublication.Title);
        var normalizedVenue = Normalize(newPublication.Venue.Name);

        var candidatePool = await publications.ListByPublicationYearAsync(year, cancellationToken).ConfigureAwait(false);
        var match = candidatePool.FirstOrDefault(p =>
            p.Id != newPublication.Id &&
            p.MergedIntoPublicationId is null &&
            Normalize(p.Title) == normalizedTitle &&
            Normalize(p.Venue.Name) == normalizedVenue);

        if (match is null)
        {
            return null;
        }

        var candidate = PublicationDuplicateCandidate.Flag(
            newPublication.Id.Value,
            match.Id.Value,
            $"Normalized title/venue/year match: '{normalizedTitle}' @ '{normalizedVenue}' ({year}).",
            clock.UtcNow);

        candidates.Add(candidate);
        return candidate;
    }

    /// <summary>Lower-cased, whitespace-collapsed, punctuation-stripped - deliberately simple, see class remarks.</summary>
    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;

        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
