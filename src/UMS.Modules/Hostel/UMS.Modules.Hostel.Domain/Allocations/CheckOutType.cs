namespace UMS.Modules.Hostel.Domain.Allocations;

/// <summary>requirement-spec.md §2 Check-out: "Voluntary (student-requested, mid-session), end-of-session (bulk), or disciplinary (officer-initiated)".</summary>
public enum CheckOutType
{
    Voluntary,
    EndOfSession,
    Disciplinary,
}
