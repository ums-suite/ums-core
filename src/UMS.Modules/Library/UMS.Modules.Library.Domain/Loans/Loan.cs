using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Loans;

/// <summary>
/// LIB-5..8: docs/ddd/ubiquitous-language.md - "binds one BookCopy to one borrower."
/// requirement-spec.md §3: <c>Active → Returned</c> (or <see cref="LoanStatus.LostWriteOff"/>).
///
/// <para>
/// <b>Concurrency mechanism - read this before changing <see cref="Renew"/>, <see cref="Return"/>, or
/// <see cref="ForceCloseAsLost"/>.</b> design-decisions.md "Concurrent Return/Renewal Conflict
/// Contract": every writer of this aggregate is only ever called by an Application service after it
/// has taken a pessimistic <c>SELECT ... FOR UPDATE</c> lock on this row - the transaction that
/// acquires the lock SECOND re-checks <see cref="Status"/> post-lock (this class's own in-memory
/// guards) and surfaces a distinguishable <c>Conflict</c> error rather than silently overwriting or
/// no-oping (edge-cases.md "Concurrent return and renewal requests for the same Loan").
/// </para>
///
/// <para>
/// <see cref="IsOverdue"/> is a pure computed function of <see cref="DueDate"/>/<see cref="Status"/>,
/// never a persisted column (requirement-spec.md §4: "avoids a background job racing a concurrent
/// return/renewal write").
/// </para>
/// </summary>
public sealed class Loan : AggregateRoot<LoanId>
{
    private Loan()
    {
    }

    private Loan(LoanId id, Guid bookCopyId, Guid bookId, Guid borrowerId, BorrowerType borrowerType, Guid? issuedByUserId, DateTimeOffset dueDate, DateTimeOffset now)
    {
        Id = id;
        BookCopyId = bookCopyId;
        BookId = bookId;
        BorrowerId = borrowerId;
        BorrowerType = borrowerType;
        IssuedByUserId = issuedByUserId;
        IssuedAt = now;
        DueDate = dueDate;
        RenewalCount = 0;
        Status = LoanStatus.Active;
    }

    public Guid BookCopyId { get; private set; }

    public Guid BookId { get; private set; }

    public Guid BorrowerId { get; private set; }

    public BorrowerType BorrowerType { get; private set; }

    public Guid? IssuedByUserId { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset DueDate { get; private set; }

    public int RenewalCount { get; private set; }

    public LoanStatus Status { get; private set; }

    public DateTimeOffset? ReturnedAt { get; private set; }

    public DateTimeOffset? LostWriteOffAt { get; private set; }

    /// <summary>LIB-5: called only after the caller has taken the target BookCopy's row lock and confirmed every borrower-eligibility gate in the SAME transaction (design-decisions.md "Fine-Blocks-New-Loan Enforcement Point").</summary>
    public static Result<Loan> Issue(Guid bookCopyId, Guid bookId, Guid borrowerId, BorrowerType borrowerType, Guid? issuedByUserId, DateTimeOffset dueDate, DateTimeOffset now)
    {
        if (bookCopyId == Guid.Empty || bookId == Guid.Empty || borrowerId == Guid.Empty)
        {
            return Error.Validation("loan.identifiers_required", "A Loan requires a valid bookCopyId, bookId, and borrowerId.");
        }

        if (dueDate <= now)
        {
            return Error.Validation("loan.due_date_invalid", "A Loan's due date must be in the future.");
        }

        var loan = new Loan(LoanId.New(), bookCopyId, bookId, borrowerId, borrowerType, issuedByUserId, dueDate, now);
        loan.Raise(new LoanIssued(loan.Id.Value, bookCopyId, bookId, borrowerId, borrowerType, dueDate, now));
        return loan;
    }

    /// <summary>Overdue is deliberately computed, never persisted (see class remarks).</summary>
    public bool IsOverdue(DateTimeOffset asOf) => Status == LoanStatus.Active && DueDate < asOf;

    /// <summary>LIB-7: requirement-spec.md §4 "Renewal is rejected if an active Reservation queue is non-empty" and the max-renewal-count cap are both cross-aggregate/configuration checks the calling service makes BEFORE calling this method - this method only enforces the Loan's own state-machine precondition (must still be Active).</summary>
    public Result Renew(DateTimeOffset newDueDate, DateTimeOffset now)
    {
        if (Status != LoanStatus.Active)
        {
            return Result.Failure(Error.Conflict("loan.already_returned", $"Loan '{Id}' cannot be renewed - it is currently '{Status}' (requires 'Active')."));
        }

        if (newDueDate <= DueDate)
        {
            return Result.Failure(Error.Validation("loan.renewal_due_date_invalid", "A renewal's new due date must extend beyond the current due date."));
        }

        DueDate = newDueDate;
        RenewalCount++;
        Raise(new LoanRenewed(Id.Value, BorrowerId, newDueDate, RenewalCount, now));
        return Result.Success();
    }

    /// <summary>LIB-8: called only after the caller has taken this row's lock (symmetric with <see cref="Renew"/> - see class remarks).</summary>
    public Result Return(DateTimeOffset now)
    {
        if (Status != LoanStatus.Active)
        {
            return Result.Failure(Error.Conflict(
                Status == LoanStatus.Returned ? "loan.already_returned" : "loan.already_lost_write_off",
                $"Loan '{Id}' cannot be returned - it is currently '{Status}'."));
        }

        var wasOverdue = IsOverdue(now);
        Status = LoanStatus.Returned;
        ReturnedAt = now;
        Raise(new LoanReturned(Id.Value, BookCopyId, BookId, BorrowerId, wasOverdue, now));
        return Result.Success();
    }

    /// <summary>LIB-17: requirement-spec.md §8 "the Loan is force-closed" - the copy will never be returned through the normal path, so this is a distinct terminal transition from <see cref="Return"/>.</summary>
    public Result ForceCloseAsLost(DateTimeOffset now)
    {
        if (Status != LoanStatus.Active)
        {
            return Result.Failure(Error.Conflict(
                Status == LoanStatus.Returned ? "loan.already_returned" : "loan.already_lost_write_off",
                $"Loan '{Id}' cannot be force-closed as lost - it is currently '{Status}'."));
        }

        Status = LoanStatus.LostWriteOff;
        LostWriteOffAt = now;
        Raise(new LoanLostWriteOff(Id.Value, BookCopyId, BookId, BorrowerId, now));
        return Result.Success();
    }
}
