using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IBuildingRepository
{
    public Task<Building?> GetByIdAsync(BuildingId id, CancellationToken cancellationToken = default);

    /// <summary>Row-locked read - see <see cref="IUniversityRepository.GetByIdForUpdateAsync"/>'s own remarks. Held across a Room create's parent-check-plus-insert, or this Building's own hard-delete's zero-active-Rooms check.</summary>
    public Task<Building?> GetByIdForUpdateAsync(BuildingId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Building>> ListAsync(CampusId? campusId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(CampusId? campusId, CancellationToken cancellationToken = default);

    public void Add(Building building);

    public void Remove(Building building);
}
