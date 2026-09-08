using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Mentorship;
using UMS.Modules.Alumni.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.IntegrationTests.Mentorship;

/// <summary>
/// design-decisions.md "Mentor-Capacity Enforcement Mechanism"; edge-cases.md "Concurrent
/// MentorshipMatch requests for the same limited-capacity mentor" - a genuine concurrent race (real
/// <see cref="Task.WhenAll"/>, each branch its own DI scope) against the real atomic conditional
/// <c>UPDATE ... WHERE active_count &lt; capacity_limit</c>, never a sequential retry.
/// </summary>
[Collection(AlumniApiTestCollectionDefinition.Name)]
public sealed class MentorCapacityConcurrencyTests(AlumniServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_proposals_against_a_mentor_with_exactly_one_slot_remaining_exactly_one_wins()
    {
        var mentorAlumnusId = Guid.NewGuid();
        var menteeAId = Guid.NewGuid();
        var menteeBId = Guid.NewGuid();

        using (var setupScope = fixture.Services.CreateScope())
        {
            var optInService = setupScope.ServiceProvider.GetRequiredService<MentorshipOptInService>();
            var mentorOptIn = await optInService.OptInAsync(mentorAlumnusId, new OptInRequest("Mentor", "Software", 1, null));
            Assert.True(mentorOptIn.IsSuccess);

            Assert.True((await optInService.OptInAsync(menteeAId, new OptInRequest("Mentee", "Software", 0, null))).IsSuccess);
            Assert.True((await optInService.OptInAsync(menteeBId, new OptInRequest("Mentee", "Software", 0, null))).IsSuccess);
        }

        async Task<Result<MentorshipMatchDto>> ProposeAsync(Guid menteeId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<MentorshipMatchService>();
            return await service.ProposeAsync(new ProposeMatchRequest(mentorAlumnusId, menteeId));
        }

        var results = await Task.WhenAll(ProposeAsync(menteeAId), ProposeAsync(menteeBId));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "mentorshipmatch.capacity_exceeded");
    }

    [Fact]
    public async Task Ending_a_match_releases_capacity_for_a_new_proposal()
    {
        var mentorAlumnusId = Guid.NewGuid();
        var menteeAId = Guid.NewGuid();
        var menteeBId = Guid.NewGuid();

        using var scope = fixture.Services.CreateScope();
        var optInService = scope.ServiceProvider.GetRequiredService<MentorshipOptInService>();
        var matchService = scope.ServiceProvider.GetRequiredService<MentorshipMatchService>();

        await optInService.OptInAsync(mentorAlumnusId, new OptInRequest("Mentor", "Software", 1, null));
        await optInService.OptInAsync(menteeAId, new OptInRequest("Mentee", "Software", 0, null));
        await optInService.OptInAsync(menteeBId, new OptInRequest("Mentee", "Software", 0, null));

        var first = await matchService.ProposeAsync(new ProposeMatchRequest(mentorAlumnusId, menteeAId));
        Assert.True(first.IsSuccess);

        var secondBeforeEnding = await matchService.ProposeAsync(new ProposeMatchRequest(mentorAlumnusId, menteeBId));
        Assert.True(secondBeforeEnding.IsFailure);

        var rejected = await matchService.RejectAsync(first.Value.Id, new EndMatchRequest("reassigning"));
        Assert.True(rejected.IsSuccess);

        var secondAfterReleasing = await matchService.ProposeAsync(new ProposeMatchRequest(mentorAlumnusId, menteeBId));
        Assert.True(secondAfterReleasing.IsSuccess);
    }
}
