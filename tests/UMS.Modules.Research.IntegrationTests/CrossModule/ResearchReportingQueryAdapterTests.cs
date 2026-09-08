using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Application.FundingBodies;
using UMS.Modules.Research.Application.Grants;
using UMS.Modules.Research.Application.InstitutionalRepositoryEntries;
using UMS.Modules.Research.Application.Publications;
using UMS.Modules.Research.IntegrationTests.Infrastructure;
using UMS.Shared.Faculty;
using UMS.Shared.Research;

namespace UMS.Modules.Research.IntegrationTests.CrossModule;

/// <summary>
/// Flow #26: exercises <c>ResearchReportingQueryAdapter</c> - the one real implementation of
/// <see cref="IResearchReportingQuery"/> - against a real Postgres row set, mirroring this fixture's
/// own Application-service-level posture exactly (no unit test could exercise the EF ComplexProperty
/// projection this adapter relies on for Money's Amount/Currency, so this lives at the integration
/// layer instead).
/// </summary>
[Collection(ResearchApiTestCollectionDefinition.Name)]
public sealed class ResearchReportingQueryAdapterTests(ResearchServiceFixture fixture)
{
    private static AuditContext NewAudit() => new(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

    private Guid RegisterFacultyMember()
    {
        var id = Guid.NewGuid();
        fixture.FacultyMembers.Register(new FacultyMemberSummary(id, Guid.NewGuid(), "Active"));
        return id;
    }

    [Fact]
    public async Task Snapshot_counts_only_confirmed_funding_and_excludes_merged_Publications()
    {
        using var scope = fixture.Services.CreateScope();
        var fundingBodies = scope.ServiceProvider.GetRequiredService<FundingBodyService>();
        var grants = scope.ServiceProvider.GetRequiredService<GrantService>();
        var publications = scope.ServiceProvider.GetRequiredService<PublicationService>();
        var repositoryEntries = scope.ServiceProvider.GetRequiredService<InstitutionalRepositoryEntryService>();
        var query = scope.ServiceProvider.GetRequiredService<IResearchReportingQuery>();

        // Deltas, not absolute totals - this fixture's Postgres instance is shared across every test
        // in this collection (ResearchApiTestCollectionDefinition), so another test's own Grants/
        // Publications/InstitutionalRepositoryEntries may already exist in the same rows this
        // adapter counts.
        var before = await query.GetDashboardSnapshotAsync();

        var fundingBody = await fundingBodies.CreateAsync(new CreateFundingBodyRequest($"Funder {Guid.NewGuid():N}", "USA", "Government", null));
        Assert.True(fundingBody.IsSuccess);

        // Active, confirmed in USD - counts toward both TotalActiveGrants and the USD funding total.
        var active = await grants.ProposeAsync(
            new ProposeGrantRequest("Active Grant", "d", fundingBody.Value.Id, 10_000m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), RegisterFacultyMember()),
            NewAudit());
        Assert.True(active.IsSuccess);
        var funded = await grants.FundAsync(active.Value.Id, new FundGrantRequest(DateOnly.FromDateTime(DateTime.UtcNow), 50_000m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), active.Value.Version), NewAudit());
        Assert.True(funded.IsSuccess);
        var activated = await grants.ActivateAsync(active.Value.Id, funded.Value.Version, NewAudit());
        Assert.True(activated.IsSuccess);

        // Funded (not yet Active), confirmed in BDT - counts toward the BDT funding total but NOT
        // TotalActiveGrants.
        var fundedOnly = await grants.ProposeAsync(
            new ProposeGrantRequest("Funded Grant", "d", fundingBody.Value.Id, 5_000m, "BDT", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), RegisterFacultyMember()),
            NewAudit());
        Assert.True(fundedOnly.IsSuccess);
        var fundedOnlyResult = await grants.FundAsync(fundedOnly.Value.Id, new FundGrantRequest(DateOnly.FromDateTime(DateTime.UtcNow), 20_000m, "BDT", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), fundedOnly.Value.Version), NewAudit());
        Assert.True(fundedOnlyResult.IsSuccess);

        // Rejected - never confirmed, must not appear in any funding total.
        var rejected = await grants.ProposeAsync(
            new ProposeGrantRequest("Rejected Grant", "d", fundingBody.Value.Id, 999_999m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1), RegisterFacultyMember()),
            NewAudit());
        Assert.True(rejected.IsSuccess);
        var rejectedResult = await grants.RejectAsync(rejected.Value.Id, rejected.Value.Version, NewAudit());
        Assert.True(rejectedResult.IsSuccess);

        var venue = new VenueDto("Journal", "A Journal", null);
        var citation = new CitationMetadataDto(null, DateOnly.FromDateTime(DateTime.UtcNow), null);
        var survivor = await publications.CreateAsync(new CreatePublicationRequest("Survivor", [new AuthorEntryDto(1, null, "Author One", null, true)], venue, citation), NewAudit());
        Assert.True(survivor.IsSuccess);
        var duplicate = await publications.CreateAsync(new CreatePublicationRequest("Duplicate", [new AuthorEntryDto(1, null, "Author One", null, true)], venue, citation), NewAudit());
        Assert.True(duplicate.IsSuccess);
        var merged = await publications.MergeAsync(survivor.Value.Id, new MergePublicationsRequest(duplicate.Value.Id), NewAudit());
        Assert.True(merged.IsSuccess);

        var repositoryEntry = await repositoryEntries.DepositAsync(
            new DepositRepositoryEntryRequest("A Thesis", "Thesis", new ContributorDto("A Student", null), null, DateOnly.FromDateTime(DateTime.UtcNow), new EmbargoPolicyDto(false, null, "Public")),
            NewAudit());
        Assert.True(repositoryEntry.IsSuccess);

        var after = await query.GetDashboardSnapshotAsync();

        Assert.Equal(3, after.TotalGrants - before.TotalGrants);
        Assert.Equal(1, after.TotalActiveGrants - before.TotalActiveGrants);

        var beforeUsd = before.ConfirmedFundingAmountByCurrency.GetValueOrDefault("USD");
        var beforeBdt = before.ConfirmedFundingAmountByCurrency.GetValueOrDefault("BDT");

        // The Active grant's CONFIRMED (Fund-time) amount counts, not its originally-proposed one -
        // and the Rejected grant's 999,999 USD never confirmed, so it contributes nothing here.
        Assert.Equal(50_000m, after.ConfirmedFundingAmountByCurrency["USD"] - beforeUsd);
        Assert.Equal(20_000m, after.ConfirmedFundingAmountByCurrency["BDT"] - beforeBdt);

        // Only the surviving Publication is counted - the merged-away row is excluded.
        Assert.Equal(1, after.TotalPublications - before.TotalPublications);
        Assert.Equal(1, after.TotalRepositoryEntries - before.TotalRepositoryEntries);
    }
}
