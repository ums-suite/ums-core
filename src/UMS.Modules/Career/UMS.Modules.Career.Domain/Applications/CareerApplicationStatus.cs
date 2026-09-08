namespace UMS.Modules.Career.Domain.Applications;

/// <summary>
/// requirement-spec.md §2.4: one status enum shared by both targets.
/// Internship path: `Submitted -> UnderReview -> Shortlisted -> Interviewed -> Offered/Rejected`.
/// Drive path: `Submitted -> UnderReview -> Shortlisted -> InterviewScheduled (on slot booking) ->
/// Interviewed -> Offered/Rejected`. `Withdrawn` reachable from either path before a terminal status;
/// `Cancelled` is reserved exclusively for the withdrawal/cancellation cascade (§2.6) - never written
/// by the staff-decision path.
/// </summary>
public enum CareerApplicationStatus
{
    Submitted,
    UnderReview,
    Shortlisted,
    InterviewScheduled,
    Interviewed,
    Offered,
    Rejected,
    Withdrawn,
    Cancelled,
}
