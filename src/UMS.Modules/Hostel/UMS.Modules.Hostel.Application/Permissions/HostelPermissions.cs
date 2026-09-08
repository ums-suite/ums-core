namespace UMS.Modules.Hostel.Application.Permissions;

/// <summary>
/// requirement-spec.md §6 API surface, gated per ADR-0012/identity's catalog convention
/// (<c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>, at least three dot-separated segments,
/// each segment lowercase letters/digits ONLY - no underscore. A flatter string, or one with an
/// underscore inside a segment (caught the hard way: it silently passed a solo Hostel-module build
/// but broke every OTHER module's own Identity-backed integration test the moment its
/// WebApplicationFactory host tried to sync the full permission catalog at startup) is rejected by
/// Identity's permission-catalog validator.
/// </summary>
public static class HostelPermissions
{
    public const string InventoryManage = "hostel.inventory.manage";
    public const string ApplicationWindowManage = "hostel.window.manage";
    public const string ApplicationReview = "hostel.application.review";
    public const string AllocationCheckIn = "hostel.allocation.checkin";
    public const string AllocationCheckOut = "hostel.allocation.checkout";
    public const string ComplaintResolve = "hostel.complaint.resolve";
}
