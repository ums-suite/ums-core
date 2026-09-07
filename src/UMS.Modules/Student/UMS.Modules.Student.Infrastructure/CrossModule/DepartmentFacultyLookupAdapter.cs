using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Organization;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>STU-11: adapts <c>UMS.Shared.Organization.IOrganizationHierarchyQuery</c> to Student's own local port.</summary>
internal sealed class DepartmentFacultyLookupAdapter(IOrganizationHierarchyQuery organizationHierarchyQuery) : IDepartmentFacultyLookup
{
    public Task<Guid?> GetParentFacultyIdAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        organizationHierarchyQuery.GetParentFacultyIdAsync(departmentId, cancellationToken);
}
