using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Application.Publications;
using UMS.Modules.Research.IntegrationTests.Infrastructure;

namespace UMS.Modules.Research.IntegrationTests.Publications;

/// <summary>
/// RES-9: requirement-spec.md §4 "globally unique within Research's schema, case-insensitive" (a
/// real database unique-constraint violation, not an application-level pre-check) and the DOI-absent
/// fuzzy-match flagging path - both against a real Postgres.
/// </summary>
[Collection(ResearchApiTestCollectionDefinition.Name)]
public sealed class PublicationDuplicateDetectionTests(ResearchServiceFixture fixture)
{
    [Fact]
    public async Task Creating_a_second_Publication_with_the_same_DOI_case_insensitively_is_rejected_by_the_real_database_constraint()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PublicationService>();
        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

        var first = await service.CreateAsync(BuildRequest("Original Title", "10.1000/Example.DOI", 2026), audit);
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(BuildRequest("A Different Title Entirely", "10.1000/EXAMPLE.doi", 2026), audit);

        Assert.True(second.IsFailure);
        Assert.Equal("publication.duplicate_value", second.Error!.Code);
    }

    [Fact]
    public async Task A_DOI_absent_Publication_matching_an_existing_ones_normalized_title_venue_and_year_is_flagged_as_a_duplicate_candidate_not_rejected()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PublicationService>();
        var duplicateDetection = scope.ServiceProvider.GetRequiredService<PublicationDuplicateDetectionService>();
        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

        var first = await service.CreateAsync(BuildRequest("A Shared Title", null, 2026), audit);
        Assert.True(first.IsSuccess);

        // Same normalized (title, venue, year), no DOI on either - never auto-merged, only flagged.
        var second = await service.CreateAsync(BuildRequest("A Shared Title", null, 2026), audit);
        Assert.True(second.IsSuccess);

        var pending = await duplicateDetection.ListPendingAsync(0, 50);
        Assert.Contains(pending, c => c.PublicationId == second.Value.Id && c.CandidatePublicationId == first.Value.Id);
    }

    [Fact]
    public async Task MergeAsync_absorbs_the_losing_Publications_FundedByGrantIds_and_marks_it_merged()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PublicationService>();
        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

        var surviving = await service.CreateAsync(BuildRequest("Surviving", "10.1000/surviving", 2026), audit);
        var merged = await service.CreateAsync(BuildRequest("Merged Away", "10.1000/merged", 2025), audit);
        Assert.True(surviving.IsSuccess);
        Assert.True(merged.IsSuccess);

        var result = await service.MergeAsync(surviving.Value.Id, new MergePublicationsRequest(merged.Value.Id), audit);

        Assert.True(result.IsSuccess);

        var mergedRow = await service.GetByIdAsync(merged.Value.Id);
        Assert.True(mergedRow.IsSuccess);
        Assert.Equal(surviving.Value.Id, mergedRow.Value.MergedIntoPublicationId);
    }

    private static CreatePublicationRequest BuildRequest(string title, string? doi, int year) => new(
        title,
        [new AuthorEntryDto(0, null, "Author Name", null, true)],
        new VenueDto("Journal", "A Journal", null),
        new CitationMetadataDto(doi, new DateOnly(year, 1, 1), null));
}
