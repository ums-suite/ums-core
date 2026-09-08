using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.UnitTests.Hostels;

/// <summary>HOS-1: requirement-spec.md §4 "Room/building capacity never exceeded"; §9 decision 5; edge-cases.md "Reducing a Room's bed capacity while active allocations exist".</summary>
public sealed class RoomTests
{
    private static Room CreateRoom(int capacity = 2) =>
        Room.Create(BuildingId.New(), HostelId.New(), "R-101", RoomType.DoubleOccupancy, capacity, DateTimeOffset.UtcNow).Value;

    [Fact]
    public void Create_with_zero_capacity_is_rejected()
    {
        var result = Room.Create(BuildingId.New(), HostelId.New(), "R-101", RoomType.SingleOccupancy, 0, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("room.capacity_must_be_positive", result.Error!.Code);
    }

    [Fact]
    public void EnsureBedCanBeAdded_below_capacity_succeeds()
    {
        var room = CreateRoom(capacity: 2);

        var result = room.EnsureBedCanBeAdded(currentBedCount: 1);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EnsureBedCanBeAdded_at_capacity_is_rejected()
    {
        var room = CreateRoom(capacity: 2);

        var result = room.EnsureBedCanBeAdded(currentBedCount: 2);

        Assert.True(result.IsFailure);
        Assert.Equal("room.capacity_exceeded", result.Error!.Code);
    }

    [Fact]
    public void ReduceCapacity_above_active_allocation_count_succeeds()
    {
        var room = CreateRoom(capacity: 3);

        var result = room.ReduceCapacity(newCapacity: 2, activeAllocationCount: 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, room.Capacity);
    }

    [Fact]
    public void ReduceCapacity_below_active_allocation_count_is_blocked()
    {
        var room = CreateRoom(capacity: 3);

        var result = room.ReduceCapacity(newCapacity: 1, activeAllocationCount: 2);

        Assert.True(result.IsFailure);
        Assert.Equal("room.capacity_below_active_allocations", result.Error!.Code);
        Assert.Equal(3, room.Capacity);
    }

    [Fact]
    public void ReduceCapacity_equal_to_active_allocation_count_succeeds()
    {
        var room = CreateRoom(capacity: 3);

        var result = room.ReduceCapacity(newCapacity: 2, activeAllocationCount: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, room.Capacity);
    }
}
