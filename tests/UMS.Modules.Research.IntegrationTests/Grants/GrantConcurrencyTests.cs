using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Application.FundingBodies;
using UMS.Modules.Research.Application.Grants;
using UMS.Modules.Research.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.IntegrationTests.Grants;

/// <summary>
/// edge-cases.md "Concurrent investigator-list edits racing a Grant lifecycle transition";
/// design-decisions.md "Grant and Investigator-List Concurrency Control" - a genuine concurrent race
/// (real <see cref="Task.WhenAll"/>, each branch its own DI scope mirroring a distinct HTTP request),
/// never a sequential retry, against the real `xmin`-backed optimistic concurrency check. Mirrors
/// Content's own <c>NoticeConcurrencyTests</c> shape exactly.
/// </summary>
[Collection(ResearchApiTestCollectionDefinition.Name)]
public sealed class GrantConcurrencyTests(ResearchServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_AddInvestigator_calls_against_the_same_stale_version_exactly_one_wins()
    {
        var (grantId, version) = await ProposeGrantAsync();
        var coInvestigatorA = RegisterFacultyMember();
        var coInvestigatorB = RegisterFacultyMember();

        async Task<Result<GrantDto>> AddAsync(Guid facultyMemberId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<GrantService>();
            var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
            return await service.AddInvestigatorAsync(grantId, new AddGrantInvestigatorRequest(facultyMemberId, "CoInvestigator", version), audit);
        }

        var results = await Task.WhenAll(AddAsync(coInvestigatorA), AddAsync(coInvestigatorB));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "grant.concurrency_conflict");

        using var verifyScope = fixture.Services.CreateScope();
        var verifyService = verifyScope.ServiceProvider.GetRequiredService<GrantService>();
        var final = await verifyService.GetByIdAsync(grantId);
        Assert.True(final.IsSuccess);

        // Exactly one AddInvestigator committed - the PI plus exactly one Co-Investigator, never both
        // racers landing on top of the same stale version.
        Assert.Equal(2, final.Value.Investigators.Count);
    }

    [Fact]
    public async Task A_lifecycle_transition_racing_an_investigator_add_against_the_same_stale_version_exactly_one_wins()
    {
        var (grantId, proposedVersion) = await ProposeGrantAsync();
        var coInvestigator = RegisterFacultyMember();

        async Task<Result<GrantDto>> RejectAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<GrantService>();
            var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
            return await service.RejectAsync(grantId, proposedVersion, audit);
        }

        async Task<Result<GrantDto>> AddInvestigatorAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<GrantService>();
            var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
            return await service.AddInvestigatorAsync(grantId, new AddGrantInvestigatorRequest(coInvestigator, "CoInvestigator", proposedVersion), audit);
        }

        var results = await Task.WhenAll(RejectAsync(), AddInvestigatorAsync());

        // Exactly one of the two racers commits - the loser's own error code legitimately depends on
        // true interleaving timing: a genuine "read-before-the-other's-commit" race surfaces as a real
        // xmin mismatch (grant.concurrency_conflict); a "read-after" surfaces as an ordinary business-
        // rule rejection instead (grant.invalid_transition, since EnsureInvestigatorListMutable/
        // EnsureNotPiVacant already see the winner's committed status) - both are a legitimate loser
        // outcome of the same race, never both racers succeeding against the same stale version.
        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code is "grant.concurrency_conflict" or "grant.invalid_transition");
    }

    private Guid RegisterFacultyMember()
    {
        var facultyMemberId = Guid.NewGuid();
        fixture.FacultyMembers.Register(new FacultyMemberSummary(facultyMemberId, Guid.NewGuid(), "Active"));
        return facultyMemberId;
    }

    private async Task<(Guid GrantId, uint Version)> ProposeGrantAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var fundingBodyService = scope.ServiceProvider.GetRequiredService<FundingBodyService>();
        var grantService = scope.ServiceProvider.GetRequiredService<GrantService>();

        var fundingBody = await fundingBodyService.CreateAsync(new CreateFundingBodyRequest("A Foundation", "USA", "Government", null));
        Assert.True(fundingBody.IsSuccess);

        var pi = RegisterFacultyMember();
        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
        var proposed = await grantService.ProposeAsync(
            new ProposeGrantRequest("Title", "Description", fundingBody.Value.Id, 100_000m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2), pi),
            audit);
        Assert.True(proposed.IsSuccess);

        return (proposed.Value.Id, proposed.Value.Version);
    }
}
