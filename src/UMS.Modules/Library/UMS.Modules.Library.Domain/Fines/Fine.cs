using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Fines;

/// <summary>
/// LIB-11..14: docs/ddd/ubiquitous-language.md - "accrued against an overdue Loan, settled via
/// Finance." design-decisions.md "Fine-Settlement Consistency - Money-Criticality Tier": every write
/// here runs inside a real DB transaction; <see cref="Waive"/> requires a non-nullable audited actor
/// and reason, enforced as a domain invariant here, not just an application-layer convention.
///
/// <para>
/// One <see cref="Fine"/> row per <see cref="LoanId"/>/reason - for the <see cref="FineReason.Overdue"/>
/// case, <see cref="Amount"/> is the running total across every daily <see cref="FineAccrual"/>
/// increment (design-decisions.md "Fine-Accrual Job Idempotency"); for
/// <see cref="FineReason.LostReplacement"/> it is a single fixed charge set once at creation.
/// </para>
/// </summary>
public sealed class Fine : AggregateRoot<FineId>
{
    private Fine()
    {
    }

    private Fine(FineId id, Guid loanId, Guid borrowerId, BorrowerType borrowerType, FineReason reason, Money amount, DateTimeOffset now)
    {
        Id = id;
        LoanId = loanId;
        BorrowerId = borrowerId;
        BorrowerType = borrowerType;
        Reason = reason;
        Amount = amount;
        Status = FineStatus.Accruing;
        CreatedAt = now;
    }

    public Guid LoanId { get; private set; }

    public Guid BorrowerId { get; private set; }

    public BorrowerType BorrowerType { get; private set; }

    public FineReason Reason { get; private set; }

    public Money Amount { get; private set; }

    public FineStatus Status { get; private set; }

    public Guid? InvoiceId { get; private set; }

    public Guid? WaivedByUserId { get; private set; }

    public string? WaivedReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public DateTimeOffset? WaivedAt { get; private set; }

    /// <summary>LIB-11: the first day's accrual for a given overdue Loan - called only after the caller has taken the target Loan's row lock and re-checked its status is still Active post-lock (design-decisions.md).</summary>
    public static Result<Fine> AccrueNew(Guid loanId, Guid borrowerId, BorrowerType borrowerType, Money firstIncrement, DateTimeOffset now)
    {
        if (loanId == Guid.Empty || borrowerId == Guid.Empty)
        {
            return Error.Validation("fine.identifiers_required", "A Fine requires a valid loanId and borrowerId.");
        }

        var fine = new Fine(FineId.New(), loanId, borrowerId, borrowerType, FineReason.Overdue, firstIncrement, now);
        fine.Raise(new FineAccrued(fine.Id.Value, loanId, borrowerId, firstIncrement.Amount, firstIncrement.Amount, firstIncrement.Currency, now));
        return fine;
    }

    /// <summary>LIB-17: a single, fixed replacement-cost charge - not an incrementally-accruing Overdue fine.</summary>
    public static Result<Fine> CreateReplacementCharge(Guid loanId, Guid borrowerId, BorrowerType borrowerType, Money amount, DateTimeOffset now)
    {
        if (loanId == Guid.Empty || borrowerId == Guid.Empty)
        {
            return Error.Validation("fine.identifiers_required", "A Fine requires a valid loanId and borrowerId.");
        }

        var fine = new Fine(FineId.New(), loanId, borrowerId, borrowerType, FineReason.LostReplacement, amount, now);
        fine.Raise(new FineAccrued(fine.Id.Value, loanId, borrowerId, amount.Amount, amount.Amount, amount.Currency, now));
        return fine;
    }

    /// <summary>LIB-11: a subsequent day's accrual increment against this same, still-open Fine.</summary>
    public Result IncreaseAccrual(Money increment, DateTimeOffset now)
    {
        if (Status != FineStatus.Accruing)
        {
            return Result.Failure(Error.Conflict("fine.not_accruing", $"Fine '{Id}' cannot accrue further - it is currently '{Status}'."));
        }

        Amount = Amount.Add(increment);
        Raise(new FineAccrued(Id.Value, LoanId, BorrowerId, increment.Amount, Amount.Amount, Amount.Currency, now));
        return Result.Success();
    }

    /// <summary>LIB-13: recorded once Finance's CreateInvoice call (design-decisions.md: a direct, transaction-coupled in-process command) returns.</summary>
    public Result InitiateSettlement(Guid invoiceId, DateTimeOffset now)
    {
        if (Status != FineStatus.Accruing)
        {
            return Result.Failure(Error.Conflict("fine.not_accruing", $"Fine '{Id}' cannot be settled - it is currently '{Status}' (requires 'Accruing')."));
        }

        Status = FineStatus.PendingSettlement;
        InvoiceId = invoiceId;
        return Result.Success();
    }

    /// <summary>LIB-13: Finance's PaymentCompleted (PaymentSucceeded), filtered to this Fine's own InvoiceId. Idempotent - a replayed event against an already-Paid/Waived Fine is a no-op.</summary>
    public Result MarkPaid(DateTimeOffset now)
    {
        if (Status is FineStatus.Paid or FineStatus.Waived)
        {
            return Result.Success();
        }

        if (Status != FineStatus.PendingSettlement)
        {
            return Result.Failure(Error.Conflict("fine.not_pending_settlement", $"Fine '{Id}' cannot be marked paid - it is currently '{Status}'."));
        }

        Status = FineStatus.Paid;
        SettledAt = now;
        Raise(new FineSettled(Id.Value, LoanId, BorrowerId, Amount.Amount, Amount.Currency, now));
        return Result.Success();
    }

    /// <summary>LIB-14: requirement-spec.md §4 "A Fine waiver requires an audited actor + reason (Money-criticality tier)" - enforced here as a domain invariant, not left to the caller's discipline.</summary>
    public Result Waive(Guid waivedByUserId, string reason, DateTimeOffset now)
    {
        if (Status is FineStatus.Paid or FineStatus.Waived)
        {
            return Result.Failure(Error.Conflict("fine.already_settled", $"Fine '{Id}' is already '{Status}' and cannot be waived."));
        }

        if (waivedByUserId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("fine.waived_by_required", "A Fine waiver requires the acting user's id."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("fine.waiver_reason_required", "A Fine waiver requires a reason."));
        }

        Status = FineStatus.Waived;
        WaivedByUserId = waivedByUserId;
        WaivedReason = reason.Trim();
        WaivedAt = now;
        Raise(new FineWaived(Id.Value, LoanId, BorrowerId, waivedByUserId, WaivedReason, now));
        return Result.Success();
    }
}
