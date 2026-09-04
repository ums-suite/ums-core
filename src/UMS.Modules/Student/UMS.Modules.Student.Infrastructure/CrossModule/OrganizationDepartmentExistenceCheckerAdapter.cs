using UMS.Modules.Student.Application.Abstractions;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>Adapts <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c> to Student's own local port - mirrors Faculty's own <c>OrganizationDepartmentExistenceCheckerAdapter</c> exactly.</summary>
internal sealed class OrganizationDepartmentExistenceCheckerAdapter(UMS.Shared.Organization.IOrganizationNodeExistenceChecker organizationChecker) : IOrganizationDepartmentExistenceChecker
{
    public Task<bool> ExistsAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        organizationChecker.ExistsAsync(departmentId, cancellationToken);
}
