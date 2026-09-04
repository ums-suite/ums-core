using UMS.Modules.Organization.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Organization.Infrastructure.Authorization;

/// <summary>Organization's own contribution to the platform-wide Permission catalog (requirement-spec.md organization §2/§6). Mirrors <c>IdentityPermissionManifest</c> exactly - collected by Identity's own <c>PermissionCatalogService.SynchronizeAsync</c> via <c>IEnumerable&lt;IPermissionManifest&gt;</c>, regardless of which module's own <c>AddXModule</c> registered it.</summary>
internal sealed class OrganizationPermissionManifest : IPermissionManifest
{
    public string OwningModule => "organization";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(OrganizationPermissions.UniversityManage, "Create and update the University record."),
        new(OrganizationPermissions.CampusManage, "Create and update Campuses."),
        new(OrganizationPermissions.FacultyManage, "Create, rename, and deactivate Faculties."),
        new(OrganizationPermissions.DepartmentManage, "Create, rename, and deactivate Departments."),
        new(OrganizationPermissions.ProgramManage, "Create, rename, and deactivate Program records."),
        new(OrganizationPermissions.DesignationManage, "Create Designations."),
        new(OrganizationPermissions.FacilityManage, "Create Buildings/Rooms and hard-delete a Room or Building with zero downstream references."),
    ];
}
