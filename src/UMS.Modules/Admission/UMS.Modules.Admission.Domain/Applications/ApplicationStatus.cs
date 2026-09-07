namespace UMS.Modules.Admission.Domain.Applications;

/// <summary>
/// requirement-spec.md §2's `Draft → Submitted → Locked` lifecycle, extended with the confirmation
/// tail (§2 Admission Confirmation → Handoff) and a terminal `Declined` state for the waitlist
/// (ADM-22). This build collapses the literal `Submitted` state into the same transition as
/// `Locked` (§2: "transitions to Submitted then immediately Locked" - both stamped in the one
/// state-guarded conditional update, see <see cref="Application.Lock"/>'s own remarks) - a
/// documented simplification, not a dropped requirement: <see cref="Application.SubmittedAt"/> and
/// <see cref="Application.LockedAt"/> are both still recorded, just never independently observable
/// as two separate persisted statuses.
/// </summary>
public enum ApplicationStatus
{
    Draft,
    Locked,
    Confirmed,
    Declined,
}
