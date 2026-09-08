using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.IntegrationTests.Allocations;

/// <summary>
/// HOS-7: edge-cases.md "Oversell under concurrent bed allocation" - a genuine concurrent race
/// (real <see cref="Task.WhenAll"/>, each branch its own DI scope mirroring a distinct HTTP request),
/// not a sequential retry, against the real pessimistic Bed row lock + the partial-unique-index
/// backstop (design-decisions.md "Bed-Allocation Concurrency Control Pattern").
/// </summary>
[Collection(HostelApiTestCollectionDefinition.Name)]
public sealed class AllocationConcurrencyTests(HostelServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_approvals_targeting_the_same_last_available_Bed_result_in_exactly_one_Allocation()
    {
        var (hostelId, _, _) = await HostelTestData.CreateHostelWithSingleBedRoomAsync(fixture.Services);
        var windowId = await HostelTestData.CreateOpenApplicationWindowAsync(fixture.Services);

        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var applicationAId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, studentAId, hostelId, windowId);
        var applicationBId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, studentBId, hostelId, windowId);

        async Task<Result<AllocationDto>> ApproveAsync(Guid applicationId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AllocationService>();
            return await service.ApproveAndAllocateAsync(applicationId, officerUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        }

        var results = await Task.WhenAll(ApproveAsync(applicationAId), ApproveAsync(applicationBId));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "allocation.no_bed_available");
    }

    [Fact]
    public async Task Losing_side_of_the_Bed_allocation_race_can_be_retried_and_returns_to_the_ranked_queue()
    {
        // edge-cases.md's own residual note: "the application returns to the ranked queue rather
        // than silently disappearing" - the losing HostelApplication must still be Ranked (not
        // consumed/corrupted) so a retry (this same call, once a Bed frees up) can still succeed.
        var (hostelId, _, _) = await HostelTestData.CreateHostelWithSingleBedRoomAsync(fixture.Services);
        var windowId = await HostelTestData.CreateOpenApplicationWindowAsync(fixture.Services);

        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var applicationAId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, studentAId, hostelId, windowId);
        var applicationBId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, studentBId, hostelId, windowId);

        async Task<Result<AllocationDto>> ApproveAsync(Guid applicationId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AllocationService>();
            return await service.ApproveAndAllocateAsync(applicationId, officerUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        }

        var results = await Task.WhenAll(ApproveAsync(applicationAId), ApproveAsync(applicationBId));
        var loserApplicationId = results[0].IsFailure ? applicationAId : applicationBId;

        using var verifyScope = fixture.Services.CreateScope();
        var applications = verifyScope.ServiceProvider.GetRequiredService<UMS.Modules.Hostel.Application.Applications.HostelApplicationService>();
        var loser = await applications.GetByIdAsync(loserApplicationId);

        Assert.True(loser.IsSuccess);
        Assert.Equal("Ranked", loser.Value.Status);
    }
}
