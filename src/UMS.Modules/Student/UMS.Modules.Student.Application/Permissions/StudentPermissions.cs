namespace UMS.Modules.Student.Application.Permissions;

/// <summary>requirement-spec.md student §6's endpoint table (ADR-0006). <c>CreateStudentRecord</c> (STU-1) is internal-only and carries no HTTP permission string at all - see <c>UMS.Shared.Student.IStudentRecordProvisioner</c>'s own remarks.</summary>
public static class StudentPermissions
{
    /// <summary>Admin/Registrar read of any Student profile by id (§6 <c>GET /students/{id}</c>), scope-checked.</summary>
    public const string ProfileRead = "student.profile.read";

    /// <summary>Admin/Registrar status transitions (§6 <c>POST /students/{id}/status</c>) - audited, reason required.</summary>
    public const string StatusChange = "student.status.change";

    /// <summary>
    /// STU-12/STU-13/STU-14 (§6 <c>GET/.../approve/.../reject /students/requests/{id}</c>) - Department
    /// Head/Registrar. The permission-catalog validator requires 3+ dot-separated segments (this
    /// module's own known gotcha - a flat `studentrequest.review` as requirement-spec.md §6 literally
    /// names it does not survive validation), so this is prefixed <c>student.</c> exactly like
    /// <see cref="ProfileRead"/>/<see cref="StatusChange"/> above. Bare possession of this permission
    /// grants only university-wide (Registrar-tier) visibility; a grievance routed to a specific
    /// Department/Faculty scope additionally requires an ADR-0006 ScopeGrant covering that scope
    /// (<c>IReviewerScopeDirectory</c>) - see <c>StudentRequestService</c>'s own remarks.
    /// </summary>
    public const string RequestReview = "student.studentrequest.review";

    /// <summary>STU-15/STU-16 (§6 <c>POST/GET /students/bulk-import...</c>) - admin-only, same 3-segment-prefix rule as <see cref="RequestReview"/>.</summary>
    public const string BulkImportExecute = "student.bulkimport.execute";
}
