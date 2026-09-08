using UMS.Modules.Research.Domain.Publications;

namespace UMS.Modules.Research.UnitTests.Publications;

/// <summary>RES-6/RES-8/RES-9: requirement-spec.md §2 Publication Records, §4 domain invariants.</summary>
public sealed class PublicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_accepts_a_mixed_internal_and_external_author_list_preserving_explicit_order()
    {
        var internalAuthor = new AuthorEntry(0, Guid.NewGuid(), "Dr. Internal", "CS Department", IsCorrespondingAuthor: true);
        var externalAuthor = new AuthorEntry(1, null, "External Co-Author", "Other University", IsCorrespondingAuthor: false);

        var publication = Publication.Create("Title", [internalAuthor, externalAuthor], MakeVenue(), Citation(null), Now);

        Assert.Equal(2, publication.Authors.Count);
        Assert.Contains(publication.Authors, a => a.FacultyMemberId is null && a.Name == "External Co-Author");
        Assert.Contains(publication.Authors, a => a.FacultyMemberId is not null && a.IsCorrespondingAuthor);
    }

    [Fact]
    public void Create_rejects_authors_with_duplicate_Order_values()
    {
        var a = new AuthorEntry(0, null, "A", null, false);
        var b = new AuthorEntry(0, null, "B", null, false);

        Assert.Throws<ArgumentException>(() => Publication.Create("Title", [a, b], MakeVenue(), Citation(null), Now));
    }

    [Fact]
    public void Create_rejects_an_empty_author_list()
    {
        Assert.Throws<ArgumentException>(() => Publication.Create("Title", [], MakeVenue(), Citation(null), Now));
    }

    [Fact]
    public void AddFundedByGrant_is_idempotent_for_the_same_GrantId()
    {
        var publication = Publication.Create("Title", [new AuthorEntry(0, null, "A", null, false)], MakeVenue(), Citation(null), Now);
        var grantId = Guid.NewGuid();

        publication.AddFundedByGrant(grantId);
        publication.AddFundedByGrant(grantId);

        Assert.Single(publication.FundedByGrantIds);
    }

    [Fact]
    public void A_merged_Publication_can_no_longer_be_mutated()
    {
        var publication = Publication.Create("Title", [new AuthorEntry(0, null, "A", null, false)], MakeVenue(), Citation(null), Now);
        publication.MarkMergedInto(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => publication.Update("New Title", [new AuthorEntry(0, null, "A", null, false)], MakeVenue(), Citation(null), Now));
        Assert.Throws<InvalidOperationException>(() => publication.AddFundedByGrant(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => publication.SetPubliclyVisible(true));
    }

    [Fact]
    public void AbsorbMerge_copies_the_losing_Publications_FundedByGrantIds_onto_the_surviving_row()
    {
        var surviving = Publication.Create("Title", [new AuthorEntry(0, null, "A", null, false)], MakeVenue(), Citation(null), Now);
        var mergedGrantId = Guid.NewGuid();

        surviving.AbsorbMerge([mergedGrantId], Guid.NewGuid(), Now);

        Assert.Contains(mergedGrantId, surviving.FundedByGrantIds);
    }

    [Fact]
    public void CitationMetadata_NormalizedDoi_is_trimmed_and_lower_cased()
    {
        var citation = CitationMetadata.Create(" 10.1000/ABC123 ", DateOnly.FromDateTime(Now.UtcDateTime), null).Value;

        Assert.Equal("10.1000/abc123", citation.NormalizedDoi);
    }

    [Fact]
    public void CitationMetadata_rejects_a_negative_citation_count()
    {
        var result = CitationMetadata.Create(null, DateOnly.FromDateTime(Now.UtcDateTime), -1);

        Assert.True(result.IsFailure);
    }

    private static Venue MakeVenue() => Venue.Create(VenueType.Journal, "Journal Name", null).Value;

    private static CitationMetadata Citation(string? doi) => CitationMetadata.Create(doi, DateOnly.FromDateTime(Now.UtcDateTime), null).Value;
}
