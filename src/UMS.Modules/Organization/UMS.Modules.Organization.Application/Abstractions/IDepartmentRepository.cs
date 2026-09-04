using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IDepartmentRepository
{
    public Task<Department?> GetByIdAsync(DepartmentId id, CancellationToken cancellationToken = default);

    /// <summary>Row-locked read - see <see cref="IUniversityRepository.GetByIdForUpdateAsync"/>'s own remarks.</summary>
    public Task<Department?> GetByIdForUpdateAsync(DepartmentId id, CancellationToken cancellationToken = default);

    public Task<bool> HasAnyActiveUnderAsync(FacultyId facultyId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Department>> ListAsync(FacultyId? facultyId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(FacultyId? facultyId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken cancellationToken = default);

    public void Add(Department department);
}
