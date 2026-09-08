using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Catalog;

/// <summary>
/// LIB-1: requirement-spec.md §3 - "the unit a Loan actually binds to; carries its own status." This
/// entity itself is the single source of truth for availability - never derived from
/// <c>library.loans</c> the way Hostel's <c>Bed</c> derives occupancy from <c>Allocation</c> rows,
/// since a <see cref="BookCopy"/> also passes through <see cref="BookCopyStatus.Reserved"/> (a state
/// with no corresponding Loan row at all).
///
/// <para>
/// <b>Concurrency mechanism - read this before changing any writer of this aggregate.</b>
/// design-decisions.md "Copy-Issuance Concurrency Control Pattern": every writer that changes a
/// BookCopy's circulation state - issuance (<see cref="MarkOnLoan"/>), return
/// (<see cref="MarkAvailable"/>), reservation-offer (<see cref="MarkReserved"/>), and lost write-off
/// (<see cref="MarkLost"/>) - is only ever called by an Application service after it has taken a
/// pessimistic <c>SELECT ... FOR UPDATE</c> lock on this row, with the partial unique index on
/// <c>library.loans</c> (<c>UNIQUE (book_copy_id) WHERE status = 'Active'</c>) retained as the
/// DB-level backstop - defense-in-depth, never either/or (requirement-spec.md §4).
/// </para>
/// </summary>
public sealed class BookCopy : AggregateRoot<BookCopyId>
{
    private BookCopy()
    {
    }

    private BookCopy(BookCopyId id, BookId bookId, string accessionNumber, string condition, CopyType copyType, DateTimeOffset now)
    {
        Id = id;
        BookId = bookId;
        AccessionNumber = accessionNumber;
        Condition = condition;
        CopyType = copyType;
        Status = BookCopyStatus.Available;
        CreatedAt = now;
    }

    public BookId BookId { get; private set; }

    public string AccessionNumber { get; private set; } = string.Empty;

    public string Condition { get; private set; } = string.Empty;

    public CopyType CopyType { get; private set; }

    public BookCopyStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LostAt { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public static Result<BookCopy> Create(BookId bookId, string accessionNumber, string condition, CopyType copyType, DateTimeOffset now)
    {
        if (bookId.Value == Guid.Empty)
        {
            return Error.Validation("book_copy.book_id_required", "A BookCopy requires a valid bookId.");
        }

        if (string.IsNullOrWhiteSpace(accessionNumber))
        {
            return Error.Validation("book_copy.accession_number_required", "A BookCopy's accession/barcode number is required.");
        }

        return new BookCopy(BookCopyId.New(), bookId, accessionNumber.Trim(), string.IsNullOrWhiteSpace(condition) ? "Good" : condition.Trim(), copyType, now);
    }

    /// <summary>LIB-5/LIB-15: called only after the caller has taken this row's lock (see class remarks). Accepts a copy already <see cref="BookCopyStatus.Reserved"/> (the queued patron's own claim window) as well as one freshly <see cref="BookCopyStatus.Available"/> (a walk-in issuance) - which of the two is legal for a given borrower is a cross-aggregate check the calling service makes against the Reservation table, not this method.</summary>
    public Result MarkOnLoan()
    {
        if (Status is not (BookCopyStatus.Available or BookCopyStatus.Reserved))
        {
            return Result.Failure(Error.Conflict("book_copy.not_available", $"BookCopy '{Id}' cannot be loaned out - it is currently '{Status}'."));
        }

        Status = BookCopyStatus.OnLoan;
        return Result.Success();
    }

    /// <summary>LIB-8: symmetric with <see cref="MarkOnLoan"/> - called only after the caller has taken this row's lock.</summary>
    public Result MarkAvailable()
    {
        if (Status != BookCopyStatus.OnLoan)
        {
            return Result.Failure(Error.Conflict("book_copy.not_on_loan", $"BookCopy '{Id}' cannot be returned - it is currently '{Status}' (requires 'OnLoan')."));
        }

        Status = BookCopyStatus.Available;
        return Result.Success();
    }

    /// <summary>LIB-9: reservation fulfillment holds a just-freed copy for one specific queued Reservation's bounded claim window, taking it out of general circulation without yet creating a Loan.</summary>
    public Result MarkReserved()
    {
        if (Status != BookCopyStatus.Available)
        {
            return Result.Failure(Error.Conflict("book_copy.not_available", $"BookCopy '{Id}' cannot be held for a Reservation - it is currently '{Status}' (requires 'Available')."));
        }

        Status = BookCopyStatus.Reserved;
        return Result.Success();
    }

    /// <summary>LIB-9: a claim-window expiry with no other queued Reservation to offer to - returns the copy to general availability.</summary>
    public Result ReleaseReservationHold()
    {
        if (Status != BookCopyStatus.Reserved)
        {
            return Result.Failure(Error.Conflict("book_copy.not_reserved", $"BookCopy '{Id}' has no Reservation hold to release - it is currently '{Status}'."));
        }

        Status = BookCopyStatus.Available;
        return Result.Success();
    }

    /// <summary>LIB-17: requirement-spec.md §8 "a BookCopy is reported lost mid-loan → ... the copy is marked Lost." Reachable from any non-terminal status (a Lost report can also target a copy sitting Available/Reserved on the shelf, not only one mid-loan).</summary>
    public Result MarkLost(DateTimeOffset now)
    {
        if (Status is BookCopyStatus.Lost or BookCopyStatus.Withdrawn)
        {
            return Result.Failure(Error.Conflict("book_copy.already_terminal", $"BookCopy '{Id}' is already '{Status}' and cannot be reported lost."));
        }

        Status = BookCopyStatus.Lost;
        LostAt = now;
        Raise(new BookCopyReportedLost(Id.Value, BookId.Value, now));
        return Result.Success();
    }

    public Result Withdraw(DateTimeOffset now)
    {
        if (Status != BookCopyStatus.Available)
        {
            return Result.Failure(Error.Conflict("book_copy.not_available", $"BookCopy '{Id}' cannot be withdrawn - it is currently '{Status}' (requires 'Available')."));
        }

        Status = BookCopyStatus.Withdrawn;
        WithdrawnAt = now;
        return Result.Success();
    }
}
