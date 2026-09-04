using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface ICampusRepository
{
    public Task<Campus?> GetByIdAsync(CampusId id, CancellationToken cancellationToken = default);

    /// <summary>Row-locked read - see <see cref="IUniversityRepository.GetByIdForUpdateAsync"/>'s own remarks.</summary>
    public Task<Campus?> GetByIdForUpdateAsync(CampusId id, CancellationToken cancellationToken = default);

    public Task<bool> HasAnyActiveUnderAsync(UniversityId universityId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Campus>> ListAsync(UniversityId? universityId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(UniversityId? universityId, CancellationToken cancellationToken = default);

    public void Add(Campus campus);
}
