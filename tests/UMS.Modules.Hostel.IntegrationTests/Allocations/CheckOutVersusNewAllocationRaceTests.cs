using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.IntegrationTests.Infrastructure;

namespace UMS.Modules.Hostel.IntegrationTests.Allocations;

/// <summary>
/// HOS-13 vs HOS-7: edge-cases.md "Check-out and a new allocation racing for the same bed" -
/// design-decisions.md's symmetric lock-extension: check-out takes the SAME <c>SELECT ... FOR
/// UPDATE</c> Bed-row lock as allocation creation, so the two writers serialize rather than double-
/// book the Bed. A real concurrent <see cref="Task.WhenAll"/> against Postgres, not a sequential call.
/// </summary>
[Collection(HostelApiTestCollectionDefinition.Name)]
public sealed class CheckOutVersusNewAllocationRaceTests(HostelServiceFixture fixture)
{
    [Fact]
    public async Task Concurrent_checkout_and_new_allocation_attempt_never_double_book_the_freed_Bed()
    {
        var (hostelId, _, _) = await HostelTestData.CreateHostelWithSingleBedRoomAsync(fixture.Services);
        var windowId = await HostelTestData.CreateOpenApplicationWindowAsync(fixture.Services);

        var incumbentStudentId = Guid.NewGuid();
        var incumbentApplicationId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, incumbentStudentId, hostelId, windowId);

        using (var approveScope = fixture.Services.CreateScope())
        {
            var initialAllocationService = approveScope.ServiceProvider.GetRequiredService<AllocationService>();
            var approved = await initialAllocationService.ApproveAndAllocateAsync(incumbentApplicationId, Guid.NewGuid(), Guid.NewGuid().ToString());
            Assert.True(approved.IsSuccess);
        }

        var incumbentAllocationId = await GetAllocationIdAsync(incumbentApplicationId);

        var challengerStudentId = Guid.NewGuid();
        var challengerApplicationId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, challengerStudentId, hostelId, windowId);

        async Task<UMS.Shared.ErrorHandling.Results.Result<AllocationDto>> CheckOutAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AllocationService>();
            return await service.CheckOutAsync(incumbentAllocationId, CheckOutType.Voluntary, Guid.NewGuid(), Guid.NewGuid().ToString());
        }

        async Task<UMS.Shared.ErrorHandling.Results.Result<AllocationDto>> ApproveChallengerAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AllocationService>();
            return await service.ApproveAndAllocateAsync(challengerApplicationId, Guid.NewGuid(), Guid.NewGuid().ToString());
        }

        var results = await Task.WhenAll(CheckOutAsync(), ApproveChallengerAsync());
        var checkOutResult = results[0];
        var approveResult = results[1];

        // Check-out itself never contends with anything that would make IT fail - it is always the
        // side that either wins the lock first (and always succeeds) or loses it (and still always
        // succeeds once the lock is released, since nothing else touches this specific Allocation row).
        Assert.True(checkOutResult.IsSuccess);

        using var verifyScope = fixture.Services.CreateScope();
        var allocationService = verifyScope.ServiceProvider.GetRequiredService<AllocationService>();
        var incumbentAfter = (await allocationService.GetByIdAsync(incumbentAllocationId)).Value;
        Assert.Equal("CheckedOut", incumbentAfter.Status);

        if (approveResult.IsSuccess)
        {
            // The challenger could only have won the SAME single Bed after the incumbent's
            // check-out had already committed - proof the lock serialized the two writers rather
            // than letting them both believe the Bed was free simultaneously.
            Assert.Equal(incumbentAfter.BedId, approveResult.Value.BedId);
            Assert.Equal("Pending", approveResult.Value.Status);
        }
        else
        {
            Assert.Equal("allocation.no_bed_available", approveResult.Error!.Code);
        }
    }

    private async Task<Guid> GetAllocationIdAsync(Guid applicationId)
    {
        using var scope = fixture.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<UMS.Modules.Hostel.Application.Applications.HostelApplicationService>();
        var application = (await applications.GetByIdAsync(applicationId)).Value;
        return application.AllocationId!.Value;
    }
}
