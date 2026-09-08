namespace UMS.Modules.Career.Application.Permissions;

/// <summary>
/// requirement-spec.md §7: permission strings namespaced `career.*`, each checked against Identity's
/// own catalog validator (`^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$` - at least 3 dot-separated
/// segments, each starting with a letter, no underscores) before being committed to.
///
/// <para>
/// Self-service Student actions (browse, apply, register, book/cancel a slot, withdraw, manage own
/// ResumeProfiles) are deliberately NOT gated by a dedicated Permission here - they require only a
/// live session plus an ownership check in the Api layer, mirroring Alumni's own self-service
/// posture exactly. Only staff/Admin-only actions get a Permission string (design-decisions.md
/// "Employer Identity Model": "every mutation to these three carries a real Career-Services-staff or
/// Admin actor").
/// </para>
/// </summary>
public static class CareerPermissions
{
    public const string EmployerManage = "career.employer.manage";

    public const string InternshipManage = "career.internship.manage";

    public const string DriveManage = "career.drive.manage";
}
