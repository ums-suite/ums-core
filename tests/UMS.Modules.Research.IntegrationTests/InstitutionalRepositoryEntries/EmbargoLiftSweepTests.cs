using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Application.InstitutionalRepositoryEntries;

namespace UMS.Modules.Research.IntegrationTests.InstitutionalRepositoryEntries;

/// <summary>
/// RES-12: design-decisions.md "InstitutionalRepositoryEntry Embargo-Lift Mechanism" - a lapsed
/// embargo is lifted by the sweep service (what <c>ResearchEmbargoLiftSweepWorker</c> calls on its
/// own daily tick), never by a lazy read-time check, and a still-in-the-future embargo is left alone.
/// </summary>
[Collection(Infrastructure.ResearchApiTestCollectionDefinition.Name)]
public sealed class EmbargoLiftSweepTests(Infrastructure.ResearchServiceFixture fixture)
{
    [Fact]
    public async Task LiftLapsedEmbargoesAsync_lifts_an_entry_whose_EmbargoEndDate_has_passed_but_leaves_a_future_one_alone()
    {
        using var scope = fixture.Services.CreateScope();
        var entryService = scope.ServiceProvider.GetRequiredService<InstitutionalRepositoryEntryService>();
        var embargoLiftService = scope.ServiceProvider.GetRequiredService<EmbargoLiftService>();
        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

        var lapsed = await entryService.DepositAsync(BuildRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)), audit);
        var future = await entryService.DepositAsync(BuildRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30)), audit);
        Assert.True(lapsed.IsSuccess);
        Assert.True(future.IsSuccess);

        var liftedCount = await embargoLiftService.LiftLapsedEmbargoesAsync(batchSize: 100, correlationId: "test-sweep");

        Assert.Equal(1, liftedCount);

        var lapsedAfter = await entryService.GetByIdAsync(lapsed.Value.Id);
        var futureAfter = await entryService.GetByIdAsync(future.Value.Id);
        Assert.False(lapsedAfter.Value.Embargo.IsEmbargoed);
        Assert.True(futureAfter.Value.Embargo.IsEmbargoed);
    }

    [Fact]
    public async Task A_lifted_entry_becomes_visible_on_the_public_listing()
    {
        using var scope = fixture.Services.CreateScope();
        var entryService = scope.ServiceProvider.GetRequiredService<InstitutionalRepositoryEntryService>();
        var embargoLiftService = scope.ServiceProvider.GetRequiredService<EmbargoLiftService>();
        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

        var lapsed = await entryService.DepositAsync(BuildRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)), audit);
        Assert.True(lapsed.IsSuccess);

        await embargoLiftService.LiftLapsedEmbargoesAsync(batchSize: 100, correlationId: "test-sweep");

        var publicPage = await entryService.ListPublicAsync(0, 50);
        Assert.Contains(publicPage.Items, e => e.Id == lapsed.Value.Id);
    }

    private static DepositRepositoryEntryRequest BuildRequest(DateOnly embargoEndDate) => new(
        "A Thesis",
        "Thesis",
        new ContributorDto("Graduate Student", null),
        null,
        DateOnly.FromDateTime(DateTime.UtcNow),
        new EmbargoPolicyDto(true, embargoEndDate, "Restricted"));
}
