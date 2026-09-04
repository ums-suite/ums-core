using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.Application.Hierarchy;

/// <summary>
/// Walks the University→Campus→Faculty→Department→Program chain in both directions purely to
/// compute which <see cref="IOrganizationTreeCache"/> keys a structural mutation must invalidate
/// (design-decisions.md, "Caching Strategy for the Hierarchy Tree": "explicit, targeted
/// invalidation ... not a full-cache flush"). Every method here does a handful of extra reads
/// against tables holding, per requirement-spec.md organization §2's own scale reference, a few
/// hundred rows total - negligible against this module's deliberately low write volume (§5).
/// </summary>
public sealed class HierarchyAncestryResolver(
    ICampusRepository campuses,
    IFacultyRepository faculties,
    IDepartmentRepository departments,
    IProgramRepository programs)
{
    public async Task<IReadOnlyList<Guid>> GetCampusAncestorsAsync(CampusId campusId, CancellationToken cancellationToken)
    {
        var campus = await campuses.GetByIdAsync(campusId, cancellationToken).ConfigureAwait(false);
        return campus is null ? [] : [campus.UniversityId.Value];
    }

    public async Task<IReadOnlyList<Guid>> GetFacultyAncestorsAsync(FacultyId facultyId, CancellationToken cancellationToken)
    {
        var faculty = await faculties.GetByIdAsync(facultyId, cancellationToken).ConfigureAwait(false);
        if (faculty is null)
        {
            return [];
        }

        var campusAncestors = await GetCampusAncestorsAsync(faculty.CampusId, cancellationToken).ConfigureAwait(false);
        return [faculty.CampusId.Value, .. campusAncestors];
    }

    public async Task<IReadOnlyList<Guid>> GetDepartmentAncestorsAsync(DepartmentId departmentId, CancellationToken cancellationToken)
    {
        var department = await departments.GetByIdAsync(departmentId, cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return [];
        }

        var facultyAncestors = await GetFacultyAncestorsAsync(department.FacultyId, cancellationToken).ConfigureAwait(false);
        return [department.FacultyId.Value, .. facultyAncestors];
    }

    /// <summary>Every currently-known Department and Program under this Faculty - invalidated too on a Faculty rename, since each one's own ancestor-path breadcrumb display includes the renamed Faculty's name.</summary>
    public async Task<IReadOnlyList<Guid>> GetFacultyDescendantIdsAsync(FacultyId facultyId, CancellationToken cancellationToken)
    {
        var departmentList = await departments.ListAsync(facultyId, 0, int.MaxValue, cancellationToken).ConfigureAwait(false);
        var ids = new List<Guid>(departmentList.Select(d => d.Id.Value));
        foreach (var department in departmentList)
        {
            ids.AddRange(await GetDepartmentDescendantIdsAsync(department.Id, cancellationToken).ConfigureAwait(false));
        }

        return ids;
    }

    /// <summary>Every Program under this Department - see <see cref="GetFacultyDescendantIdsAsync"/>'s own remarks.</summary>
    public async Task<IReadOnlyList<Guid>> GetDepartmentDescendantIdsAsync(DepartmentId departmentId, CancellationToken cancellationToken)
    {
        var programList = await programs.ListAsync(departmentId, 0, int.MaxValue, cancellationToken).ConfigureAwait(false);
        return programList.Select(p => p.Id.Value).ToList();
    }
}
