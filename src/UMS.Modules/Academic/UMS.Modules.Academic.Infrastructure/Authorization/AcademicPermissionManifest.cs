using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Academic.Infrastructure.Authorization;

/// <summary>Academic's own contribution to the platform-wide Permission catalog (requirement-spec.md §6). Mirrors Organization/Faculty's own PermissionManifest exactly.</summary>
internal sealed class AcademicPermissionManifest : IPermissionManifest
{
    public string OwningModule => "academic";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(AcademicPermissions.ProgramManage, "Create/manage a Program."),
        new(AcademicPermissions.CurriculumManage, "Create/version a Curriculum."),
        new(AcademicPermissions.CourseManage, "Create/manage a Course."),
        new(AcademicPermissions.AcademicSessionManage, "Create an AcademicSession/Semester."),
        new(AcademicPermissions.CourseOfferingManage, "Create a CourseOffering and assign its Instructor."),
        new(AcademicPermissions.EnrollmentApprove, "Advisor approval gate on a Pending Enrollment."),
        new(AcademicPermissions.AttendanceRecord, "Record per-session attendance (Instructor)."),
        new(AcademicPermissions.GradeEnter, "Enter/submit marks for a Grade (Faculty)."),
        new(AcademicPermissions.GradeLock, "Lock or reject a submitted grade batch (Department Head review)."),
        new(AcademicPermissions.GradeCorrect, "Correct a locked/published Grade (Registrar-approved workflow)."),
        new(AcademicPermissions.ResultApprove, "Approve a Verified grade batch before publication (authorized authority)."),
        new(AcademicPermissions.ResultPublish, "Publish (or archive) an Approved grade batch."),
        new(AcademicPermissions.StudentResultRead, "Read a Student's results/transcript."),
    ];
}
