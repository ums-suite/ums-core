namespace UMS.Modules.Faculty.Application.Permissions;

/// <summary>requirement-spec.md faculty §2 Permission Strings (ADR-0006). Instructor assignment, attendance, and grade permissions are Academic's own strings, checked there, not duplicated here (§2).</summary>
public static class FacultyPermissions
{
    public const string ProfileRead = "faculty.profile.read";
    public const string ProfileUpdate = "faculty.profile.update";
    public const string MemberManage = "faculty.member.manage";
    public const string CourseAssignmentRead = "faculty.courseassignment.read";
    public const string LeaveCreate = "faculty.leave.create";
    public const string LeaveApproveDepartment = "faculty.leave.approve.department";
    public const string LeaveApproveAuthority = "faculty.leave.approve.authority";
    public const string ResearchUpdate = "faculty.research.update";
    public const string ResearchPublish = "faculty.research.publish";
}
