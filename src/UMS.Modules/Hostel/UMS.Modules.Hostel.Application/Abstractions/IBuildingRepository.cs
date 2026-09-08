using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IBuildingRepository
{
    public Task<Building?> GetByIdAsync(BuildingId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Building>> GetByHostelAsync(HostelId hostelId, CancellationToken cancellationToken = default);

    public void Add(Building building);
}
