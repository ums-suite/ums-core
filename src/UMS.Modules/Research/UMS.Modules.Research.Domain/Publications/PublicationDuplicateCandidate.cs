using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Publications;

/// <summary>
/// requirement-spec.md §3 module-local term: "A flagged pair of Publication rows the system
/// believes describe the same underlying work (matched DOI, or a fuzzy title/venue/year match
/// absent a DOI), held for Admin review and merge - never auto-merged" (§8, §9). Its own small
/// aggregate root, not owned by either Publication, since it references two Publications by id
/// (design-decisions.md "Publication Duplicate Detection and Merge Mechanism").
///
/// <para>
/// Residual note carried forward, not resolved here (edge-cases.md): "the concrete fuzzy-match
/// threshold/algorithm is left to the implementation pass" - <c>PublicationDuplicateDetectionService</c>
/// implements a normalized-equality heuristic (lower-cased/punctuation-stripped title + venue name +
/// publication year all matching exactly) rather than a genuine fuzzy/Levenshtein distance, a
/// deliberately simple first pass flagged here rather than silently assumed to be more sophisticated
/// than it is.
/// </para>
/// </summary>
public sealed class PublicationDuplicateCandidate : AggregateRoot<PublicationDuplicateCandidateId>
{
    private PublicationDuplicateCandidate()
    {
    }

    private PublicationDuplicateCandidate(PublicationDuplicateCandidateId id, Guid publicationId, Guid candidatePublicationId, string matchReason, DateTimeOffset now)
    {
        Id = id;
        PublicationId = publicationId;
        CandidatePublicationId = candidatePublicationId;
        MatchReason = matchReason;
        Status = PublicationDuplicateCandidateStatus.Pending;
        CreatedAt = now;
    }

    public Guid PublicationId { get; private set; }

    public Guid CandidatePublicationId { get; private set; }

    public string MatchReason { get; private set; } = string.Empty;

    public PublicationDuplicateCandidateStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public static PublicationDuplicateCandidate Flag(Guid publicationId, Guid candidatePublicationId, string matchReason, DateTimeOffset now) =>
        new(PublicationDuplicateCandidateId.New(), publicationId, candidatePublicationId, matchReason, now);

    public void MarkResolved(DateTimeOffset now)
    {
        Status = PublicationDuplicateCandidateStatus.Resolved;
        ResolvedAt = now;
    }

    public void Dismiss(DateTimeOffset now)
    {
        Status = PublicationDuplicateCandidateStatus.Dismissed;
        ResolvedAt = now;
    }
}
