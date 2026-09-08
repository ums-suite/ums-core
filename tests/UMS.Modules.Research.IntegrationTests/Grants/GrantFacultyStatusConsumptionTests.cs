using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Application.FundingBodies;
using UMS.Modules.Research.Application.Grants;
using UMS.Modules.Research.IntegrationTests.Infrastructure;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.IntegrationTests.Grants;

/// <summary>
/// RES-5: edge-cases.md "Principal Investigator Leaves the University Mid-Grant" - a genuine
/// consumed-event test proving a real row inserted into <c>faculty."OutboxMessages"</c> (the exact
/// physical shape <c>FacultyOutboxEventSource</c> queries via raw SQL, per its own remarks) actually
/// flags a Funded/Active Grant through the real relay pipeline: <see cref="IFacultyStatusEventSource"/>
/// (real <c>FacultyOutboxEventSource</c>, resolved from the fixture's own DI container) -&gt;
/// <see cref="GrantPiVacancyService"/> - never a fake/mocked event source.
/// </summary>
[Collection(ResearchApiTestCollectionDefinition.Name)]
public sealed class GrantFacultyStatusConsumptionTests(ResearchServiceFixture fixture)
{
    [Fact]
    public async Task A_real_FacultyMemberStatusChanged_row_in_Facultys_own_outbox_table_flags_the_PIs_Funded_Grant()
    {
        var pi = Guid.NewGuid();
        fixture.FacultyMembers.Register(new FacultyMemberSummary(pi, Guid.NewGuid(), "Active"));

        var (grantId, _) = await ProposeAndFundAsync(pi);

        // The exact envelope shape FacultyOutboxEventSource's own remarks describe: EF-default
        // PascalCase naming, a full-CLR-type-name EventType value matched via a `LIKE
        // '%.FacultyMemberStatusChanged'` suffix, FacultyMemberId inside PayloadJson.
        var eventId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { FacultyMemberId = pi, NewStatus = "Separated" });
        await fixture.InsertFacultyOutboxMessageAsync(
            eventId,
            "UMS.Modules.Faculty.Domain.Events.FacultyMemberStatusChanged",
            payload,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        using (var relayScope = fixture.Services.CreateScope())
        {
            var eventSource = relayScope.ServiceProvider.GetRequiredService<IFacultyStatusEventSource>();
            var vacancyService = relayScope.ServiceProvider.GetRequiredService<GrantPiVacancyService>();

            var envelopes = await eventSource.GetUnprocessedAsync(batchSize: 50);
            var envelope = Assert.Single(envelopes);
            Assert.Equal(pi, envelope.FacultyMemberId);

            var flagged = await vacancyService.ApplyFacultyStatusChangeAsync(envelope.FacultyMemberId, $"FacultyMemberStatusChanged:{envelope.EventId}");
            Assert.Equal(1, flagged);

            await eventSource.MarkProcessedAsync(envelope.EventId);
        }

        using (var verifyScope = fixture.Services.CreateScope())
        {
            var grantService = verifyScope.ServiceProvider.GetRequiredService<GrantService>();
            var grant = await grantService.GetByIdAsync(grantId);
            Assert.True(grant.IsSuccess);
            Assert.True(grant.Value.RequiresPiReassignment);
        }

        using (var idempotencyScope = fixture.Services.CreateScope())
        {
            // The same event, once marked processed, is never re-delivered - the relay's own
            // exactly-once-per-envelope contract.
            var eventSource = idempotencyScope.ServiceProvider.GetRequiredService<IFacultyStatusEventSource>();
            var envelopes = await eventSource.GetUnprocessedAsync(batchSize: 50);
            Assert.Empty(envelopes);
        }
    }

    [Fact]
    public async Task Reassigning_a_PI_via_AddInvestigator_clears_the_flag_raised_by_the_consumed_event()
    {
        var pi = Guid.NewGuid();
        var newPi = Guid.NewGuid();
        fixture.FacultyMembers.Register(new FacultyMemberSummary(pi, Guid.NewGuid(), "Active"));
        fixture.FacultyMembers.Register(new FacultyMemberSummary(newPi, Guid.NewGuid(), "Active"));

        var (grantId, _) = await ProposeAndFundAsync(pi);

        using (var scope = fixture.Services.CreateScope())
        {
            var vacancyService = scope.ServiceProvider.GetRequiredService<GrantPiVacancyService>();
            var flagged = await vacancyService.ApplyFacultyStatusChangeAsync(pi, "test-correlation");
            Assert.Equal(1, flagged);
        }

        Guid grantIdForReassignment;
        uint versionForReassignment;
        using (var scope = fixture.Services.CreateScope())
        {
            var grantService = scope.ServiceProvider.GetRequiredService<GrantService>();
            var grant = await grantService.GetByIdAsync(grantId);
            Assert.True(grant.Value.RequiresPiReassignment);
            grantIdForReassignment = grant.Value.Id;
            versionForReassignment = grant.Value.Version;
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var grantService = scope.ServiceProvider.GetRequiredService<GrantService>();
            var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
            var reassigned = await grantService.AddInvestigatorAsync(grantIdForReassignment, new AddGrantInvestigatorRequest(newPi, "PrincipalInvestigator", versionForReassignment), audit);
            Assert.True(reassigned.IsSuccess);
            Assert.False(reassigned.Value.RequiresPiReassignment);
            Assert.Equal(newPi, reassigned.Value.PrincipalInvestigatorFacultyMemberId);
        }
    }

    private async Task<(Guid GrantId, uint Version)> ProposeAndFundAsync(Guid principalInvestigatorFacultyMemberId)
    {
        using var scope = fixture.Services.CreateScope();
        var fundingBodyService = scope.ServiceProvider.GetRequiredService<FundingBodyService>();
        var grantService = scope.ServiceProvider.GetRequiredService<GrantService>();

        var fundingBody = await fundingBodyService.CreateAsync(new CreateFundingBodyRequest("A Foundation", "USA", "Government", null));
        Assert.True(fundingBody.IsSuccess);

        var audit = new AuditContext(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());
        var proposed = await grantService.ProposeAsync(
            new ProposeGrantRequest("Title", "Description", fundingBody.Value.Id, 100_000m, "USD", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2), principalInvestigatorFacultyMemberId),
            audit);
        Assert.True(proposed.IsSuccess);

        var funded = await grantService.FundAsync(
            proposed.Value.Id,
            new FundGrantRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100_000m, "USD", proposed.Value.FundingPeriodStart, proposed.Value.FundingPeriodEnd, proposed.Value.Version),
            audit);
        Assert.True(funded.IsSuccess);

        return (funded.Value.Id, funded.Value.Version);
    }
}
