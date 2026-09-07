namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>STU-11: Student's own local port onto <c>UMS.Shared.Organization.IOrganizationHierarchyQuery</c> - the one-level walk-up a grievance's own-Department-Head escalation needs (edge-cases.md).</summary>
public interface IDepartmentFacultyLookup
{
    public Task<Guid?> GetParentFacultyIdAsync(Guid departmentId, CancellationToken cancellationToken = default);
}
