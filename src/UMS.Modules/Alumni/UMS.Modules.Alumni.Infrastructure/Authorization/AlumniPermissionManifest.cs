using UMS.Modules.Alumni.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Alumni.Infrastructure.Authorization;

internal sealed class AlumniPermissionManifest : IPermissionManifest
{
    public string OwningModule => "alumni";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(AlumniPermissions.ChapterManage, "Create/manage an AlumniChapter and its own events (Admin/chapter-lead)."),
        new(AlumniPermissions.JobModerate, "Approve/reject/remove any JobPosting (Admin)."),
        new(AlumniPermissions.DirectoryReadPrivate, "Bypass directory visibility filtering to read a Private Alumnus's profile (Admin/support, itself an audited access)."),
        new(AlumniPermissions.MentorshipCoordinate, "Propose/reject a MentorshipMatch pairing (Admin/coordinator)."),
        new(AlumniPermissions.DonationReconcile, "View an anonymous Donation's real donor identity for internal reconciliation/support (Finance/Alumni staff)."),
    ];
}
