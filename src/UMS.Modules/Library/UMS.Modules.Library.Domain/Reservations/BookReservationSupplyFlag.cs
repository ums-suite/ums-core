using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Reservations;

/// <summary>
/// LIB-17: design-decisions.md "Lost-Copy Write-Off vs. Active Reservation Queue" - an additive,
/// insert-only, librarian-facing signal, never an automated action against any individual
/// <see cref="Reservation"/> (edge-cases.md: "a patron's Reservation represents an active expectation
/// that shouldn't be silently revoked"). Mirrors <see cref="Loans.LoanReviewFlag"/>'s exact shape.
/// </summary>
public sealed class BookReservationSupplyFlag
{
    private BookReservationSupplyFlag()
    {
    }

    private BookReservationSupplyFlag(Guid id, Guid bookId, int remainingCopyCount, int queueDepth, string reason, string sourceEventReference, DateTimeOffset now)
    {
        Id = id;
        BookId = bookId;
        RemainingCopyCount = remainingCopyCount;
        QueueDepth = queueDepth;
        Reason = reason;
        SourceEventReference = sourceEventReference;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid BookId { get; private set; }

    public int RemainingCopyCount { get; private set; }

    public int QueueDepth { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string SourceEventReference { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<BookReservationSupplyFlag> Create(Guid bookId, int remainingCopyCount, int queueDepth, string reason, string sourceEventReference, DateTimeOffset now)
    {
        if (bookId == Guid.Empty)
        {
            return Error.Validation("book_reservation_supply_flag.book_id_required", "A BookReservationSupplyFlag requires a non-empty bookId.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("book_reservation_supply_flag.reason_required", "A BookReservationSupplyFlag's reason is required.");
        }

        return new BookReservationSupplyFlag(Guid.NewGuid(), bookId, remainingCopyCount, queueDepth, reason.Trim(), sourceEventReference, now);
    }
}
