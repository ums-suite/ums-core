namespace UMS.Modules.Organization.Application.Permissions;

/// <summary>
/// requirement-spec.md organization §2/§6: "each scoped by permission
/// (<c>organization.&lt;entity&gt;.manage</c>)" per ADR-0006's catalog convention. Shared between
/// the Infrastructure-side manifest (<c>OrganizationPermissionManifest</c>) and the Api-side
/// endpoint gating, exactly like Identity's own <c>IdentityPermissions</c>.
///
/// <para>
/// Every write endpoint (create/rename/deactivate/hard-delete) is gated by the relevant constant
/// below. Every read endpoint (`GET`) is deliberately left <c>AllowAnonymous</c> instead - §5's
/// own Caching NFR frames the hierarchy as "a natural candidate for CDN-cacheable public
/// responses (program catalog, faculty directory)," which is incompatible with gating reads
/// behind a manage-level permission; §6's table names one permission per entity without
/// distinguishing a read verb from a write verb, so there is no *narrower* read permission to
/// name instead. This mirrors the split Identity's own <c>POST /users</c> (self-registration,
/// anonymous) already establishes within one endpoint group.
/// </para>
/// </summary>
public static class OrganizationPermissions
{
    public const string UniversityManage = "organization.university.manage";
    public const string CampusManage = "organization.campus.manage";
    public const string FacultyManage = "organization.faculty.manage";
    public const string DepartmentManage = "organization.department.manage";
    public const string ProgramManage = "organization.program.manage";
    public const string DesignationManage = "organization.designation.manage";
    public const string FacilityManage = "organization.facility.manage";
}
