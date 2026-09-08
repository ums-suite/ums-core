using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Reservations;

/// <summary>
/// LIB-4/LIB-9: docs/ddd/ubiquitous-language.md - "a queued claim on a Book when all copies are out."
/// requirement-spec.md §2: "FIFO (or priority, e.g. faculty-first) queue." <see cref="Priority"/>
/// (lower sorts first) plus <see cref="CreatedAt"/> together define the fairness ordering the
/// fulfillment SQL's own <c>ORDER BY</c> selects on (design-decisions.md "Reservation-Queue Fairness
/// and Claim Mechanism") - deliberately no separate, re-numbered "queue position" column: recomputing
/// a position for every other queued Reservation whenever one leaves the queue would itself become a
/// write-contention point this design avoids entirely by sorting on two already-stable fields instead.
///
/// <para>
/// <see cref="Offer"/> is called ONLY by the fulfillment job's own atomic conditional SQL UPDATE
/// (design-decisions.md), which bypasses this aggregate's own change-tracked <c>Raise</c> - the
/// corresponding <see cref="Events.ReservationFulfilled"/> event is therefore enqueued directly via
/// <c>IDomainEventRecorder</c> by the calling service, not by this method. <see cref="Offer"/> exists
/// here purely as the in-memory representation of that same transition for anything that loads and
/// inspects an already-Offered row afterward (e.g. the claim path, the expiry sweep).
/// </para>
/// </summary>
public sealed class Reservation : AggregateRoot<ReservationId>
{
    private Reservation()
    {
    }

    private Reservation(ReservationId id, Guid bookId, Guid borrowerId, BorrowerType borrowerType, int priority, DateTimeOffset now)
    {
        Id = id;
        BookId = bookId;
        BorrowerId = borrowerId;
        BorrowerType = borrowerType;
        Priority = priority;
        Status = ReservationStatus.Queued;
        CreatedAt = now;
    }

    public Guid BookId { get; private set; }

    public Guid BorrowerId { get; private set; }

    public BorrowerType BorrowerType { get; private set; }

    /// <summary>Lower sorts first - requirement-spec.md §2's "priority, e.g. faculty-first" is implemented as Faculty=0, Student=1.</summary>
    public int Priority { get; private set; }

    public ReservationStatus Status { get; private set; }

    public Guid? OfferedCopyId { get; private set; }

    public DateTimeOffset? OfferedAt { get; private set; }

    public DateTimeOffset? ClaimWindowExpiresAt { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>LIB-4: called only after the calling service has verified, under the same BookCopy-row-lock discipline as issuance, that zero copies of this Book are currently Available (requirement-spec.md §4).</summary>
    public static Result<Reservation> Create(Guid bookId, Guid borrowerId, BorrowerType borrowerType, DateTimeOffset now)
    {
        if (bookId == Guid.Empty || borrowerId == Guid.Empty)
        {
            return Error.Validation("reservation.identifiers_required", "A Reservation requires a valid bookId and borrowerId.");
        }

        var priority = borrowerType == BorrowerType.Faculty ? 0 : 1;
        var reservation = new Reservation(ReservationId.New(), bookId, borrowerId, borrowerType, priority, now);
        reservation.Raise(new ReservationCreated(reservation.Id.Value, bookId, borrowerId, borrowerType, now));
        return reservation;
    }

    /// <summary>See class remarks - the in-memory mirror of the fulfillment job's own atomic SQL UPDATE, not itself a change-tracked write path.</summary>
    public Result Offer(Guid bookCopyId, DateTimeOffset claimWindowExpiresAt, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Queued)
        {
            return Result.Failure(Error.Conflict("reservation.not_queued", $"Reservation '{Id}' cannot be offered a copy - it is currently '{Status}' (requires 'Queued')."));
        }

        Status = ReservationStatus.Offered;
        OfferedCopyId = bookCopyId;
        OfferedAt = now;
        ClaimWindowExpiresAt = claimWindowExpiresAt;
        return Result.Success();
    }

    /// <summary>LIB-9's own residual note: "claiming a reservation offer is, mechanically, just issuing a Loan against a BookCopy that happens to be pre-reserved" - called by the Loan-issuance path once the new Loan has been created against <see cref="OfferedCopyId"/>.</summary>
    public Result Claim(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Offered)
        {
            return Result.Failure(Error.Conflict("reservation.not_offered", $"Reservation '{Id}' cannot be claimed - it is currently '{Status}' (requires 'Offered')."));
        }

        if (ClaimWindowExpiresAt is { } expiry && expiry <= now)
        {
            return Result.Failure(Error.Conflict("reservation.claim_window_expired", $"Reservation '{Id}''s claim window has already expired."));
        }

        Status = ReservationStatus.Claimed;
        ClaimedAt = now;
        return Result.Success();
    }

    /// <summary>LIB-9: requirement-spec.md §8 "a Reservation expires unclaimed → the freed copy is offered to the next queued Reservation automatically."</summary>
    public Result Expire(Guid bookCopyId, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Offered)
        {
            return Result.Failure(Error.Conflict("reservation.not_offered", $"Reservation '{Id}' cannot expire - it is currently '{Status}' (requires 'Offered')."));
        }

        Status = ReservationStatus.Expired;
        ExpiredAt = now;
        Raise(new ReservationExpired(Id.Value, BookId, bookCopyId, BorrowerId, now));
        return Result.Success();
    }
}
