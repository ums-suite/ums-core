using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IRoomRepository
{
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Room-Capacity-Reduction Enforcement Point": pessimistic lock shared by the bed-provisioning-time capacity check and the capacity-reduction check.</summary>
    public Task<Room?> GetByIdForUpdateAsync(RoomId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Room>> GetByBuildingAsync(BuildingId buildingId, CancellationToken cancellationToken = default);

    /// <summary>Candidate Rooms for the bed-allocation search (HOS-7): every Room of the given Hostel/RoomType, cheapest-first no ordering guarantee beyond room number.</summary>
    public Task<IReadOnlyList<Room>> GetByHostelAndTypeAsync(HostelId hostelId, RoomType type, CancellationToken cancellationToken = default);

    public void Add(Room room);
}
