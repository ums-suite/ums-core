namespace UMS.Modules.Hostel.Application.Permissions;

/// <summary>
/// requirement-spec.md §6 API surface, gated per ADR-0012/identity's catalog convention
/// (<c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>, at least three dot-separated segments -
/// a flatter string is rejected by Identity's permission-catalog validator).
/// </summary>
public static class HostelPermissions
{
    public const string InventoryManage = "hostel.inventory.manage";
    public const string ApplicationWindowManage = "hostel.application_window.manage";
    public const string ApplicationReview = "hostel.application.review";
    public const string AllocationCheckIn = "hostel.allocation.checkin";
    public const string AllocationCheckOut = "hostel.allocation.checkout";
    public const string ComplaintResolve = "hostel.complaint.resolve";
}
