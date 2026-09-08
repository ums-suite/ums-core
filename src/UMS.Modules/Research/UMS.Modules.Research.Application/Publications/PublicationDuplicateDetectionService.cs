using System.Text;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Publications;
using UMS.Shared.ErrorHandling.Results;

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
public sealed class PublicationDuplicateDetectionService(IPublicationRepository publications, IPublicationDuplicateCandidateRepository candidates, IUnitOfWork unitOfWork, IClock clock)
{
    /// <summary>RES-9: the Admin review-queue read - "held for Admin review" (§3 module-local term) needs a way to list what is currently pending.</summary>
    public async Task<IReadOnlyList<PublicationDuplicateCandidateDto>> ListPendingAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await candidates.ListPendingAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }

    /// <summary>Explicit Admin dismissal of a flagged pair as NOT actually duplicates - a deliberate human decision, never automatic, mirroring the "never auto-merge" posture (§9). A genuine duplicate is instead resolved by calling <c>PublicationService.MergeAsync</c> directly; this candidate row is then left <c>Pending</c> (a documented gap - see PR notes) unless an Admin separately dismisses it here.</summary>
    public async Task<Result> DismissAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await candidates.GetByIdAsync(new PublicationDuplicateCandidateId(candidateId), cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return Result.Failure(Error.NotFound("publicationduplicatecandidate.not_found", $"No PublicationDuplicateCandidate exists with id '{candidateId}'."));
        }

        candidate.Dismiss(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

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

    private static PublicationDuplicateCandidateDto ToDto(PublicationDuplicateCandidate candidate) => new(
        candidate.Id.Value,
        candidate.PublicationId,
        candidate.CandidatePublicationId,
        candidate.MatchReason,
        candidate.Status.ToString(),
        candidate.CreatedAt);

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
