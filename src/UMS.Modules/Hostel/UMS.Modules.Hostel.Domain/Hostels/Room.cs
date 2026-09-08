using UMS.Modules.Hostel.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Hostels;

/// <summary>
/// HOS-1: requirement-spec.md §2 Inventory Management ("Room (room number, type, capacity)"). Two
/// distinct, separately-enforced invariants both key off <see cref="Capacity"/> (requirement-spec.md
/// §4, §9 decision 5; edge-cases.md "Reducing a Room's bed capacity while active allocations exist"):
///
/// <list type="bullet">
/// <item><description><see cref="EnsureBedCanBeAdded"/> - bed-provisioning-time check: a Room cannot
/// have more <see cref="Bed"/> rows than <see cref="Capacity"/>. Called by the Bed-creation service
/// under the same <see cref="Room"/> row lock as the capacity-reduction check below.</description></item>
/// <item><description><see cref="ReduceCapacity"/> - Room-edit-time check: capacity may never be
/// reduced below the Room's current active-allocation count. Both checks share the same <c>Room</c>
/// row lock (design-decisions.md "Room-Capacity-Reduction Enforcement Point") so a concurrent
/// bed-add and capacity-reduction can never leave the invariant violated between the two checks.
/// </description></item>
/// </list>
/// </summary>
public sealed class Room : AggregateRoot<RoomId>
{
    private Room()
    {
    }

    private Room(RoomId id, BuildingId buildingId, HostelId hostelId, string roomNumber, RoomType type, int capacity, DateTimeOffset now)
    {
        Id = id;
        BuildingId = buildingId;
        HostelId = hostelId;
        RoomNumber = roomNumber;
        Type = type;
        Capacity = capacity;
        CreatedAt = now;
    }

    public BuildingId BuildingId { get; private set; }

    public HostelId HostelId { get; private set; }

    public string RoomNumber { get; private set; } = string.Empty;

    public RoomType Type { get; private set; }

    public int Capacity { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Room> Create(BuildingId buildingId, HostelId hostelId, string roomNumber, RoomType type, int capacity, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            return Error.Validation("room.number_required", "A Room's room number is required.");
        }

        if (capacity <= 0)
        {
            return Error.Validation("room.capacity_must_be_positive", "A Room's capacity must be a positive number.");
        }

        return new Room(RoomId.New(), buildingId, hostelId, roomNumber.Trim(), type, capacity, now);
    }

    /// <summary>Bed-provisioning-time check (requirement-spec.md §4) - called under this Room's own row lock, immediately before a new <see cref="Bed"/> row is inserted.</summary>
    public Result EnsureBedCanBeAdded(int currentBedCount) =>
        currentBedCount >= Capacity
            ? Result.Failure(Error.Conflict("room.capacity_exceeded", $"Room '{Id}' already has {currentBedCount} Bed(s) against a capacity of {Capacity}."))
            : Result.Success();

    /// <summary>Room-edit-time check (requirement-spec.md §9 decision 5) - called under this Room's own row lock. Increasing capacity is always allowed; decreasing is blocked outright if it would fall below the current active-allocation count.</summary>
    public Result ReduceCapacity(int newCapacity, int activeAllocationCount)
    {
        if (newCapacity <= 0)
        {
            return Result.Failure(Error.Validation("room.capacity_must_be_positive", "A Room's capacity must be a positive number."));
        }

        if (newCapacity < activeAllocationCount)
        {
            return Result.Failure(Error.Conflict(
                "room.capacity_below_active_allocations",
                $"Room '{Id}' cannot be reduced to capacity {newCapacity} - it currently has {activeAllocationCount} active Allocation(s)."));
        }

        Capacity = newCapacity;
        return Result.Success();
    }
}
