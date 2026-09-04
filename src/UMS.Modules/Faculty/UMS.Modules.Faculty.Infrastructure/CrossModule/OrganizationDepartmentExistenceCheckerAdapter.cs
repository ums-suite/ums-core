using UMS.Modules.Faculty.Application.Abstractions;

namespace UMS.Modules.Faculty.Infrastructure.CrossModule;

/// <summary>Adapts <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c> to Faculty's own local port - mirrors Identity's own <c>OrganizationNodeExistenceCheckerAdapter</c> pattern exactly.</summary>
internal sealed class OrganizationDepartmentExistenceCheckerAdapter(UMS.Shared.Organization.IOrganizationNodeExistenceChecker organizationChecker) : IOrganizationDepartmentExistenceChecker
{
    public Task<bool> ExistsAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        organizationChecker.ExistsAsync(departmentId, cancellationToken);
}
