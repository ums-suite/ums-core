using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Application.Hostels;
using UMS.Modules.Hostel.IntegrationTests.Infrastructure;

namespace UMS.Modules.Hostel.IntegrationTests.Allocations;

/// <summary>HOS-1: requirement-spec.md §9 decision 5; edge-cases.md "Reducing a Room's bed capacity while active allocations exist" - the transactional, Room-row-locked block.</summary>
[Collection(HostelApiTestCollectionDefinition.Name)]
public sealed class RoomCapacityReductionTests(HostelServiceFixture fixture)
{
    [Fact]
    public async Task Reducing_capacity_below_the_current_non_terminal_allocation_count_is_blocked()
    {
        using var scope = fixture.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();

        var hostel = (await inventory.CreateHostelAsync(new CreateHostelRequest($"Hostel-{Guid.NewGuid():N}", "Mixed"))).Value;
        var building = (await inventory.CreateBuildingAsync(new CreateBuildingRequest(hostel.Id, "Building-A"))).Value;
        var room = (await inventory.CreateRoomAsync(new CreateRoomRequest(building.Id, $"R-{Guid.NewGuid():N}"[..8], "DoubleOccupancy", Capacity: 2))).Value;
        await inventory.CreateBedAsync(new CreateBedRequest(room.Id, "Bed-1"));
        await inventory.CreateBedAsync(new CreateBedRequest(room.Id, "Bed-2"));

        var windowId = await HostelTestData.CreateOpenApplicationWindowAsync(fixture.Services);
        var firstApplicationId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, Guid.NewGuid(), hostel.Id, windowId, "DoubleOccupancy");
        var secondApplicationId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, Guid.NewGuid(), hostel.Id, windowId, "DoubleOccupancy");

        var allocationService = scope.ServiceProvider.GetRequiredService<AllocationService>();
        Assert.True((await allocationService.ApproveAndAllocateAsync(firstApplicationId, Guid.NewGuid(), Guid.NewGuid().ToString())).IsSuccess);
        Assert.True((await allocationService.ApproveAndAllocateAsync(secondApplicationId, Guid.NewGuid(), Guid.NewGuid().ToString())).IsSuccess);

        // Two non-terminal (Pending) Allocations now occupy this two-bed Room - reducing to 1
        // must be blocked.
        var blocked = await inventory.ChangeRoomCapacityAsync(room.Id, new ChangeRoomCapacityRequest(NewCapacity: 1));

        Assert.True(blocked.IsFailure);
        Assert.Equal("room.capacity_below_active_allocations", blocked.Error!.Code);
    }

    [Fact]
    public async Task Reducing_capacity_to_a_value_still_covering_active_allocations_succeeds()
    {
        using var scope = fixture.Services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();

        var hostel = (await inventory.CreateHostelAsync(new CreateHostelRequest($"Hostel-{Guid.NewGuid():N}", "Mixed"))).Value;
        var building = (await inventory.CreateBuildingAsync(new CreateBuildingRequest(hostel.Id, "Building-A"))).Value;
        var room = (await inventory.CreateRoomAsync(new CreateRoomRequest(building.Id, $"R-{Guid.NewGuid():N}"[..8], "TripleOccupancy", Capacity: 3))).Value;
        await inventory.CreateBedAsync(new CreateBedRequest(room.Id, "Bed-1"));

        var allowed = await inventory.ChangeRoomCapacityAsync(room.Id, new ChangeRoomCapacityRequest(NewCapacity: 1));

        Assert.True(allowed.IsSuccess);
        Assert.Equal(1, allowed.Value.Capacity);
    }
}
