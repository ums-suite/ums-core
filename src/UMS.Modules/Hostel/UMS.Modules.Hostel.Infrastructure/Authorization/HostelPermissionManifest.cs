using UMS.Modules.Hostel.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Hostel.Infrastructure.Authorization;

internal sealed class HostelPermissionManifest : IPermissionManifest
{
    public string OwningModule => "hostel";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(HostelPermissions.InventoryManage, "Create and configure Hostel/Building/Room/Bed inventory (Hostel Officer/Admin)."),
        new(HostelPermissions.ApplicationWindowManage, "Configure the application window and eligibility rule set (Hostel Officer)."),
        new(HostelPermissions.ApplicationReview, "Review the ranked HostelApplication queue and approve/waitlist/reject (Hostel Officer)."),
        new(HostelPermissions.AllocationCheckIn, "Confirm a Student's check-in for an Allocation (Hostel Officer/front-desk)."),
        new(HostelPermissions.AllocationCheckOut, "Process an officer-initiated (end-of-session/disciplinary) check-out (Hostel Officer)."),
        new(HostelPermissions.ComplaintResolve, "Triage and resolve a Student's Complaint (Hostel Officer)."),
    ];
}
