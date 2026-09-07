using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Infrastructure.Persistence;
using UMS.Shared.Organization;

namespace UMS.Modules.Organization.Infrastructure.CrossModule;

/// <summary>The one real implementation of <see cref="IOrganizationHierarchyQuery"/> - see that interface's own remarks. Mirrors <c>OrganizationNodeExistenceChecker</c>'s composition-root registration pattern exactly.</summary>
internal sealed class OrganizationHierarchyQuery(OrganizationDbContext context) : IOrganizationHierarchyQuery
{
    public async Task<Guid?> GetParentFacultyIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var department = await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == new DepartmentId(departmentId), cancellationToken)
            .ConfigureAwait(false);

        return department?.FacultyId.Value;
    }
}
