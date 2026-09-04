using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IFacultyRepository
{
    public Task<Faculty?> GetByIdAsync(FacultyId id, CancellationToken cancellationToken = default);

    /// <summary>Row-locked read - see <see cref="IUniversityRepository.GetByIdForUpdateAsync"/>'s own remarks.</summary>
    public Task<Faculty?> GetByIdForUpdateAsync(FacultyId id, CancellationToken cancellationToken = default);

    public Task<bool> HasAnyActiveUnderAsync(CampusId campusId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Faculty>> ListAsync(CampusId? campusId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(CampusId? campusId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Faculty>> ListAllAsync(CancellationToken cancellationToken = default);

    public void Add(Faculty faculty);
}
