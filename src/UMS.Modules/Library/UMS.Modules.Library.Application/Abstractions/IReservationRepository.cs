using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.Application.Abstractions;

public interface IReservationRepository
{
    public Task<Reservation?> GetByIdAsync(ReservationId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Reservation>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default);

    /// <summary>LIB-4: requirement-spec.md §4 invariant - a borrower may hold at most one open (Queued/Offered) Reservation per Book.</summary>
    public Task<bool> HasOpenReservationAsync(Guid bookId, Guid borrowerId, CancellationToken cancellationToken = default);

    /// <summary>LIB-17: Queued + Offered count for the lost-copy write-off's own supply-vs-demand check.</summary>
    public Task<int> CountOpenByBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// LIB-9: design-decisions.md "Reservation-Queue Fairness and Claim Mechanism" - the atomic
    /// conditional SQL UPDATE that selects the next Queued Reservation for this Book (by
    /// Priority/CreatedAt fairness order) and offers it the just-freed copy, in ONE statement, so a
    /// second concurrent invocation for the same event is a race-free no-op. Returns the offered
    /// Reservation's id, or <see langword="null"/> if the queue was empty.
    /// </summary>
    public Task<Guid?> TryOfferNextQueuedAsync(Guid bookId, Guid bookCopyId, DateTimeOffset offeredAt, DateTimeOffset claimWindowExpiresAt, CancellationToken cancellationToken = default);

    /// <summary>LIB-5/LIB-9: the claim path's and the expiry sweep's shared lock - the single Offered Reservation (if any) currently holding this BookCopy. At most one Reservation is ever Offered against a given copy at a time (by construction - see <see cref="TryOfferNextQueuedAsync"/>), so no borrower filter is needed here; the caller (e.g. the claim path) checks <see cref="Reservation.BorrowerId"/> itself after locking.</summary>
    public Task<Reservation?> GetOfferedByCopyForUpdateAsync(Guid bookCopyId, CancellationToken cancellationToken = default);

    /// <summary>LIB-9: the claim-window expiry sweep's candidate query.</summary>
    public Task<IReadOnlyList<Reservation>> GetOfferedPastClaimWindowAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    public void Add(Reservation reservation);
}
