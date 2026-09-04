namespace UMS.Modules.Student.Application.Permissions;

/// <summary>requirement-spec.md student §6's endpoint table (ADR-0006). <c>CreateStudentRecord</c> (STU-1) is internal-only and carries no HTTP permission string at all - see <c>UMS.Shared.Student.IStudentRecordProvisioner</c>'s own remarks.</summary>
public static class StudentPermissions
{
    /// <summary>Admin/Registrar read of any Student profile by id (§6 <c>GET /students/{id}</c>), scope-checked.</summary>
    public const string ProfileRead = "student.profile.read";

    /// <summary>Admin/Registrar status transitions (§6 <c>POST /students/{id}/status</c>) - audited, reason required.</summary>
    public const string StatusChange = "student.status.change";
}
