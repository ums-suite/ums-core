using UMS.Modules.Career.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Career.Infrastructure.Authorization;

internal sealed class CareerPermissionManifest : IPermissionManifest
{
    public string OwningModule => "career";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(CareerPermissions.EmployerManage, "Create/manage an EmployerProfile (Career-Services staff/Admin - no employer self-service login exists)."),
        new(CareerPermissions.InternshipManage, "Create/manage an Internship posting and review its CareerApplications (Career-Services staff/Admin)."),
        new(CareerPermissions.DriveManage, "Create/manage a CampusRecruitmentDrive, its InterviewSlots, and shortlist its CareerApplications (Career-Services staff/Admin)."),
    ];
}
