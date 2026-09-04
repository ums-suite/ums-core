using UMS.Modules.Faculty.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Faculty.Infrastructure.Authorization;

/// <summary>Faculty's own contribution to the platform-wide Permission catalog (requirement-spec.md faculty §2). Mirrors Organization's own <c>OrganizationPermissionManifest</c> exactly.</summary>
internal sealed class FacultyPermissionManifest : IPermissionManifest
{
    public string OwningModule => "faculty";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(FacultyPermissions.ProfileRead, "Read a FacultyMember employment profile."),
        new(FacultyPermissions.ProfileUpdate, "Update the owning FacultyMember's own self-service profile fields."),
        new(FacultyPermissions.MemberManage, "Onboard FacultyMembers and edit any employment profile (HR/Registrar)."),
        new(FacultyPermissions.CourseAssignmentRead, "Read a FacultyMember's teaching-load / assigned-courses projection."),
        new(FacultyPermissions.LeaveCreate, "Submit a LeaveRequest."),
        new(FacultyPermissions.LeaveApproveDepartment, "Approve or reject a LeaveRequest at the Department Head step."),
        new(FacultyPermissions.LeaveApproveAuthority, "Approve or reject a LeaveRequest at the Authorized Authority step."),
        new(FacultyPermissions.ResearchUpdate, "Update any FacultyMember's ResearchProfile (HR)."),
        new(FacultyPermissions.ResearchPublish, "Publish a FacultyMember's own ResearchProfile."),
    ];
}
