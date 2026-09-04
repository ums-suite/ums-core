using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class DepartmentRepository(OrganizationDbContext context) : IDepartmentRepository
{
    public Task<Department?> GetByIdAsync(DepartmentId id, CancellationToken cancellationToken = default) =>
        context.Departments.Include(d => d.Translations).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Department?> GetByIdForUpdateAsync(DepartmentId id, CancellationToken cancellationToken = default) =>
        context.Departments
            .FromSqlInterpolated($"SELECT *, xmin FROM organization.departments WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasAnyActiveUnderAsync(FacultyId facultyId, CancellationToken cancellationToken = default) =>
        context.Departments.AnyAsync(d => d.FacultyId == facultyId && d.Status == NodeStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<Department>> ListAsync(FacultyId? facultyId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Departments
            .Include(d => d.Translations)
            .Where(d => facultyId == null || d.FacultyId == facultyId.Value)
            .OrderBy(d => d.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(FacultyId? facultyId, CancellationToken cancellationToken = default) =>
        context.Departments.CountAsync(d => facultyId == null || d.FacultyId == facultyId.Value, cancellationToken);

    public async Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await context.Departments.Include(d => d.Translations).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Department department) => context.Departments.Add(department);
}
