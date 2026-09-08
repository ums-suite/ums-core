using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Loans;

/// <summary>
/// LIB-16: mirrors Hostel's own <c>AllocationReviewFlag</c> exactly (design-decisions.md
/// "Student-Status-Change Review Flag - Additive Side-Table, Not a Loan Field", the same pattern
/// extended to Faculty's own status-change events). Deliberately NOT a field on <see cref="Loan"/>
/// and deliberately NOT an <see cref="Common.AggregateRoot{TId}"/> - a plain, insert-only record
/// keyed on <see cref="LoanId"/>, so writing it can never contend with the Loan's own state-machine
/// writes. Advisory only: it must never itself gate or force a transition (requirement-spec.md §8:
/// "flagged for a recall notice, not auto-returned - a human decision").
/// </summary>
public sealed class LoanReviewFlag
{
    private LoanReviewFlag()
    {
    }

    private LoanReviewFlag(Guid id, Guid loanId, string reason, string sourceEventReference, DateTimeOffset now)
    {
        Id = id;
        LoanId = loanId;
        Reason = reason;
        SourceEventReference = sourceEventReference;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid LoanId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string SourceEventReference { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<LoanReviewFlag> Create(Guid loanId, string reason, string sourceEventReference, DateTimeOffset now)
    {
        if (loanId == Guid.Empty)
        {
            return Error.Validation("loan_review_flag.loan_id_required", "A LoanReviewFlag requires a non-empty loanId.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("loan_review_flag.reason_required", "A LoanReviewFlag's reason is required.");
        }

        return new LoanReviewFlag(Guid.NewGuid(), loanId, reason.Trim(), sourceEventReference, now);
    }
}
