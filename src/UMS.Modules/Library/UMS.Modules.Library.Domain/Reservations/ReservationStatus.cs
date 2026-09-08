namespace UMS.Modules.Library.Domain.Reservations;

/// <summary>requirement-spec.md §2: created while queued, offered a freed copy within a bounded claim window, then either claimed (as an ordinary Loan - see <see cref="Reservation.Claim"/>) or expired back to the next in line.</summary>
public enum ReservationStatus
{
    Queued,
    Offered,
    Claimed,
    Expired,
}
