namespace UMS.Modules.Hostel.Domain.Complaints;

/// <summary>requirement-spec.md §2 Complaints: "maintenance, roommate dispute, damage, etc.".</summary>
public enum ComplaintCategory
{
    Maintenance,
    RoommateDispute,
    Damage,
    Other,
}
