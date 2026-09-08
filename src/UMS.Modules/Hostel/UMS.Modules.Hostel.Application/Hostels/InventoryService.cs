using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Hostels;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.Hostels;

/// <summary>
/// HOS-1: <c>Hostel</c>/<c>Building</c>/<c>Room</c>/<c>Bed</c> inventory CRUD and the public catalog
/// read (requirement-spec.md §2 Inventory Management, §6 <c>GET /hostels</c>).
/// </summary>
public sealed class InventoryService(
    IHostelRepository hostels,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IBedRepository beds,
    IAllocationRepository allocations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<HostelDto>> CreateHostelAsync(CreateHostelRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<HostelType>(request.Type, ignoreCase: true, out var type))
        {
            return Error.Validation("hostel.invalid_type", $"'{request.Type}' is not a recognized Hostel type.");
        }

        var created = Domain.Hostels.Hostel.Create(request.Name, type, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        hostels.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<IReadOnlyList<HostelDto>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        (await hostels.GetAllAsync(cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<Result<BuildingDto>> CreateBuildingAsync(CreateBuildingRequest request, CancellationToken cancellationToken = default)
    {
        var hostel = await hostels.GetByIdAsync(new HostelId(request.HostelId), cancellationToken).ConfigureAwait(false);
        if (hostel is null)
        {
            return Error.NotFound("hostel.not_found", $"No Hostel exists with id '{request.HostelId}'.");
        }

        var created = Building.Create(hostel.Id, request.Name, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        buildings.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<IReadOnlyList<BuildingDto>> GetBuildingsAsync(Guid hostelId, CancellationToken cancellationToken = default) =>
        (await buildings.GetByHostelAsync(new HostelId(hostelId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<Result<RoomDto>> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<RoomType>(request.Type, ignoreCase: true, out var type))
        {
            return Error.Validation("room.invalid_type", $"'{request.Type}' is not a recognized Room type.");
        }

        var building = await buildings.GetByIdAsync(new BuildingId(request.BuildingId), cancellationToken).ConfigureAwait(false);
        if (building is null)
        {
            return Error.NotFound("building.not_found", $"No Building exists with id '{request.BuildingId}'.");
        }

        var created = Room.Create(building.Id, building.HostelId, request.RoomNumber, type, request.Capacity, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        rooms.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(Guid buildingId, CancellationToken cancellationToken = default) =>
        (await rooms.GetByBuildingAsync(new BuildingId(buildingId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    /// <summary>requirement-spec.md §9 decision 5; edge-cases.md "Reducing a Room's bed capacity while active allocations exist" - the transactional, Room-row-locked check.</summary>
    public async Task<Result<RoomDto>> ChangeRoomCapacityAsync(Guid roomId, ChangeRoomCapacityRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var room = await rooms.GetByIdForUpdateAsync(new RoomId(roomId), cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("room.not_found", $"No Room exists with id '{roomId}'.");
        }

        var activeAllocationCount = await allocations.CountActiveByRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        var changed = room.ReduceCapacity(request.NewCapacity, activeAllocationCount);
        if (changed.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return changed.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(room);
    }

    /// <summary>requirement-spec.md §4: "a Room cannot have more Bed rows than its capacity value" - the transactional, Room-row-locked check, sharing the same lock as <see cref="ChangeRoomCapacityAsync"/> (design-decisions.md).</summary>
    public async Task<Result<BedDto>> CreateBedAsync(CreateBedRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var room = await rooms.GetByIdForUpdateAsync(new RoomId(request.RoomId), cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("room.not_found", $"No Room exists with id '{request.RoomId}'.");
        }

        var currentBedCount = await beds.CountByRoomAsync(room.Id, cancellationToken).ConfigureAwait(false);
        var canAdd = room.EnsureBedCanBeAdded(currentBedCount);
        if (canAdd.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return canAdd.Error!;
        }

        var created = Bed.Create(room.Id, request.Label, clock.UtcNow);
        if (created.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return created.Error!;
        }

        beds.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<IReadOnlyList<BedDto>> GetBedsAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        (await beds.GetByRoomAsync(new RoomId(roomId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    internal static HostelDto ToDto(Domain.Hostels.Hostel hostel) => new(hostel.Id.Value, hostel.Name, hostel.Type.ToString(), hostel.CreatedAt);

    internal static BuildingDto ToDto(Building building) => new(building.Id.Value, building.HostelId.Value, building.Name, building.CreatedAt);

    internal static RoomDto ToDto(Room room) => new(room.Id.Value, room.BuildingId.Value, room.HostelId.Value, room.RoomNumber, room.Type.ToString(), room.Capacity, room.CreatedAt);

    internal static BedDto ToDto(Bed bed) => new(bed.Id.Value, bed.RoomId.Value, bed.Label, bed.CreatedAt);
}
