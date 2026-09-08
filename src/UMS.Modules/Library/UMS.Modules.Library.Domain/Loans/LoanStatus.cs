namespace UMS.Modules.Library.Domain.Loans;

/// <summary>
/// requirement-spec.md §3: <c>Active → Returned</c> (or <c>Overdue</c> as a computed sub-state, never
/// a stored status - see <see cref="Loan.IsOverdue"/>), plus <see cref="LostWriteOff"/> (LIB-17: a
/// distinct terminal state from <see cref="Returned"/>, since a lost copy was never actually
/// returned). Issuance goes directly to <see cref="Active"/> - unlike Hostel's Allocation
/// (<c>Pending → FeePaid → Active</c>), there is no earlier non-terminal stage a Loan passes through
/// before <see cref="Active"/>, so <see cref="Active"/> alone is the correct partial-unique-index
/// filter for "this BookCopy already has a live Loan" (see
/// <c>Infrastructure.Persistence.Configurations.LoanConfiguration</c>'s own remarks - this is
/// deliberately NOT copy-pasting Hostel's own non-terminal-status-list correction, since that
/// correction only applies where a genuine pre-terminal intermediate status exists).
/// </summary>
public enum LoanStatus
{
    Active,
    Returned,
    LostWriteOff,
}
