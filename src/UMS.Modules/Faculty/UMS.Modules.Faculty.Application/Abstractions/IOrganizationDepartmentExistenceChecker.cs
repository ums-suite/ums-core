namespace UMS.Modules.Faculty.Application.Abstractions;

/// <summary>
/// Faculty's own local "port" onto <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c>
/// (requirement-spec.md faculty §7: "Depends on ... Organization (department/designation reference
/// data)") - mirrors the consumer-side half of Identity's own
/// <c>Application.Abstractions.IOrganizationNodeExistenceChecker</c> pattern, adapted in
/// Infrastructure to the real shared interface. Existence only, not active status - the same
/// contract the shared interface itself documents.
/// </summary>
public interface IOrganizationDepartmentExistenceChecker
{
    public Task<bool> ExistsAsync(Guid departmentId, CancellationToken cancellationToken = default);
}
