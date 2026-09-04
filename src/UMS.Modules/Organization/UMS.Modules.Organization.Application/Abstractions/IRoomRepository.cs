using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IRoomRepository
{
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken = default);

    public Task<bool> HasAnyUnderAsync(BuildingId buildingId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Room>> ListByBuildingAsync(BuildingId buildingId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountByBuildingAsync(BuildingId buildingId, CancellationToken cancellationToken = default);

    public void Add(Room room);

    public void Remove(Room room);
}
