namespace UMS.Modules.Hostel.Domain.Hostels;

/// <summary>requirement-spec.md §2: "Room (room number, type, capacity)"; also the shape of a Student's ranked room-type preference (§2 Application &amp; Allocation Workflow step 2).</summary>
public enum RoomType
{
    SingleOccupancy,
    DoubleOccupancy,
    TripleOccupancy,
    Dormitory,
}
