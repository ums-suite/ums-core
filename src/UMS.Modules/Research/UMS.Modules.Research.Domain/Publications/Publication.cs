using UMS.Modules.Research.Domain.Common;
using UMS.Modules.Research.Domain.Events;

namespace UMS.Modules.Research.Domain.Publications;

/// <summary>
/// RES-6/RES-7/RES-8/RES-9: the Publication aggregate root (requirement-spec.md §2 Publication
/// Records, §3, §4). Contains an ordered, mixed internal/external <see cref="AuthorEntry"/> list, a
/// <see cref="Venue"/>, <see cref="CitationMetadata"/>, and references zero or more <c>Grant</c>s by
/// id only (<see cref="FundedByGrantIds"/>) - never an object graph load, even though both aggregates
/// live in the <c>research</c> schema (§3, §9).
/// </summary>
public sealed class Publication : AggregateRoot<PublicationId>
{
    private readonly List<AuthorEntry> _authors = [];
    private readonly List<Guid> _fundedByGrantIds = [];

    private Publication()
    {
    }

    private Publication(PublicationId id, string title, IReadOnlyList<AuthorEntry> authors, Venue venue, CitationMetadata citation, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Venue = venue;
        Citation = citation;
        CreatedAt = now;
        _authors.AddRange(authors);
    }

    public string Title { get; private set; } = string.Empty;

    public Venue Venue { get; private set; } = null!;

    public CitationMetadata Citation { get; private set; } = null!;

    public bool IsPubliclyVisible { get; private set; }

    /// <summary>design-decisions.md's duplicate-merge decision: set only by <see cref="MarkMergedInto"/>, which happens exclusively via an Admin <c>POST .../publications/{id}/merge</c> call - never automatically. A non-null value excludes this row from every listing/read path except the merge audit trail.</summary>
    public Guid? MergedIntoPublicationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<AuthorEntry> Authors => _authors.AsReadOnly();

    public IReadOnlyCollection<Guid> FundedByGrantIds => _fundedByGrantIds.AsReadOnly();

    public static Publication Create(string title, IReadOnlyList<AuthorEntry> authors, Venue venue, CitationMetadata citation, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A Publication's title is required.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(venue);
        ArgumentNullException.ThrowIfNull(citation);
        ValidateAuthors(authors);

        return new Publication(PublicationId.New(), title.Trim(), authors, venue, citation, now);
    }

    public void Update(string title, IReadOnlyList<AuthorEntry> authors, Venue venue, CitationMetadata citation, DateTimeOffset now)
    {
        EnsureNotMerged();

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A Publication's title is required.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(venue);
        ArgumentNullException.ThrowIfNull(citation);
        ValidateAuthors(authors);

        Title = title.Trim();
        Venue = venue;
        Citation = citation;
        _authors.Clear();
        _authors.AddRange(authors);
        UpdatedAt = now;
        Raise(new PublicationUpdated(Id.Value, now));
    }

    public void SetPubliclyVisible(bool isPubliclyVisible)
    {
        EnsureNotMerged();
        IsPubliclyVisible = isPubliclyVisible;
    }

    /// <summary>
    /// requirement-spec.md §4 fifth bullet: the eligibility check ("only a Grant that has reached
    /// Funded/Active/Closed/Reported may be cited") is the CALLER's responsibility (RES-8) - this
    /// aggregate only guards against a duplicate id, since it never loads the referenced Grant.
    /// </summary>
    public void AddFundedByGrant(Guid grantId)
    {
        EnsureNotMerged();

        if (!_fundedByGrantIds.Contains(grantId))
        {
            _fundedByGrantIds.Add(grantId);
        }
    }

    public void RemoveFundedByGrant(Guid grantId)
    {
        EnsureNotMerged();
        _fundedByGrantIds.Remove(grantId);
    }

    public void RaiseRecorded(DateTimeOffset now) => Raise(new PublicationRecorded(Id.Value, now));

    /// <summary>Called on the surviving row of a merge (design-decisions.md's duplicate-merge decision) - absorbs the losing row's Grant funding links; author list/title/citation stay the surviving row's own, an explicit Admin choice of "which row is canonical", not an automatic union.</summary>
    public void AbsorbMerge(IEnumerable<Guid> mergedFundedByGrantIds, Guid mergedPublicationId, DateTimeOffset now)
    {
        EnsureNotMerged();

        foreach (var grantId in mergedFundedByGrantIds)
        {
            AddFundedByGrant(grantId);
        }

        Raise(new PublicationsMerged(Id.Value, mergedPublicationId, now));
    }

    /// <summary>Called on the losing row of a merge - see <see cref="MergedIntoPublicationId"/>'s own remarks.</summary>
    public void MarkMergedInto(Guid survivingPublicationId)
    {
        EnsureNotMerged();
        MergedIntoPublicationId = survivingPublicationId;
    }

    private static void ValidateAuthors(IReadOnlyList<AuthorEntry> authors)
    {
        if (authors is null || authors.Count == 0)
        {
            throw new ArgumentException("A Publication must have at least one author.", nameof(authors));
        }

        // requirement-spec.md §4 third bullet: "author order is never inferred from array/list
        // position alone" - enforced here by requiring every entry's own explicit Order to be
        // distinct, regardless of the order the caller happened to list them in.
        if (authors.Select(a => a.Order).Distinct().Count() != authors.Count)
        {
            throw new ArgumentException("Every AuthorEntry must have a distinct, explicit Order value.", nameof(authors));
        }

        if (authors.Any(a => string.IsNullOrWhiteSpace(a.Name)))
        {
            throw new ArgumentException("Every AuthorEntry must have a non-empty Name.", nameof(authors));
        }
    }

    private void EnsureNotMerged()
    {
        if (MergedIntoPublicationId is not null)
        {
            throw new InvalidOperationException($"This Publication has been merged into '{MergedIntoPublicationId}' and can no longer be mutated.");
        }
    }
}
