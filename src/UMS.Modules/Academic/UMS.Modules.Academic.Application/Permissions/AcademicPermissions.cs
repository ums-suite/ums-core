namespace UMS.Modules.Academic.Application.Permissions;

/// <summary>requirement-spec.md academic §6 Permission Strings (ADR-0006), flat/ScopeGrant-scoped convention. Instructor-assignment, attendance, and grade permissions are Academic's own (Faculty's own requirement-spec.md §2 explicitly defers to these, not duplicating them).</summary>
public static class AcademicPermissions
{
    public const string ProgramManage = "academic.program.manage";
    public const string CurriculumManage = "academic.curriculum.manage";
    public const string CourseManage = "academic.course.manage";
    public const string AcademicSessionManage = "academic.academicsession.manage";
    public const string CourseOfferingManage = "academic.courseoffering.manage";
    public const string EnrollmentApprove = "academic.enrollment.approve";
    public const string AttendanceRecord = "academic.attendance.record";
    public const string GradeEnter = "academic.grade.enter";
    public const string GradeLock = "academic.grade.lock";
    public const string GradeCorrect = "academic.grade.correct";
    public const string ResultApprove = "academic.result.approve";
    public const string ResultPublish = "academic.result.publish";
    public const string StudentResultRead = "academic.student.result.read";
}
