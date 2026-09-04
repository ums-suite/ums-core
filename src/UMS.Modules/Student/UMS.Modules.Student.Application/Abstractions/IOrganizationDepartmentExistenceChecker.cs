namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>
/// Student's own local "port" onto <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c>
/// (requirement-spec.md student §7: "Depends on ... Organization (Program/Department reference)") -
/// mirrors Faculty's own <c>IOrganizationDepartmentExistenceChecker</c> pattern exactly. Existence
/// only, not active status.
/// </summary>
public interface IOrganizationDepartmentExistenceChecker
{
    public Task<bool> ExistsAsync(Guid departmentId, CancellationToken cancellationToken = default);
}
