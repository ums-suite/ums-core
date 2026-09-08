using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Events;

namespace UMS.Modules.Library.Application.Reservations;

/// <summary>
/// LIB-9: design-decisions.md "Reservation-Queue Fairness and Claim Mechanism" - called by
/// <c>LibraryReservationFulfillmentRelayWorker</c> (event-driven off <see cref="LoanReturned"/>,
/// polling Library's OWN outbox, never inlined into the Return transaction itself - the same posture
/// Hostel's own <c>WaitlistReRankingService</c> takes off <c>AllocationCheckedOut</c>) and by
/// <c>LibraryReservationExpirySweepWorker</c> (a plain TTL sweep, mirroring
/// <c>GracePeriodExpiryService</c>'s own shape).
/// </summary>
public sealed class ReservationFulfillmentService(
    IBookCopyRepository bookCopies,
    IReservationRepository reservations,
    LibraryOptions options,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEventRecorder,
    IClock clock)
{
    /// <summary>
    /// Takes the BookCopy's own row lock first (design-decisions.md: "the same lock-then-conditional-
    /// write family as copy issuance"), then the atomic conditional SQL UPDATE selects and offers the
    /// next Queued Reservation in ONE statement - a second concurrent invocation for the same freed
    /// copy blocks on the row lock and then finds the copy already Reserved, a race-free no-op.
    /// </summary>
    /// <returns><see langword="true"/> if a queued Reservation was offered this copy; <see langword="false"/> if the queue was empty (or the copy was no longer Available - already claimed by a concurrent invocation/walk-in).</returns>
    public async Task<bool> FulfillAsync(Guid bookCopyId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var copy = await bookCopies.GetByIdForUpdateAsync(new BookCopyId(bookCopyId), cancellationToken).ConfigureAwait(false);
        if (copy is null || copy.Status != BookCopyStatus.Available)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var now = clock.UtcNow;
        var claimWindowExpiresAt = now.AddHours(options.ReservationClaimWindowHours);

        var offeredReservationId = await reservations.TryOfferNextQueuedAsync(copy.BookId.Value, bookCopyId, now, claimWindowExpiresAt, cancellationToken).ConfigureAwait(false);
        if (offeredReservationId is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var marked = copy.MarkReserved();
        if (marked.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var offered = await reservations.GetByIdAsync(new Domain.Reservations.ReservationId(offeredReservationId.Value), cancellationToken).ConfigureAwait(false);
        if (offered is not null)
        {
            domainEventRecorder.Enqueue(new ReservationFulfilled(offeredReservationId.Value, copy.BookId.Value, bookCopyId, offered.BorrowerId, claimWindowExpiresAt, now));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>requirement-spec.md §8 "a Reservation expires unclaimed → the freed copy is offered to the next queued Reservation automatically, not returned to general availability first."</summary>
    public async Task<int> ExpireClaimWindowsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var candidates = await reservations.GetOfferedPastClaimWindowAsync(now, batchSize, cancellationToken).ConfigureAwait(false);
        var expiredCount = 0;

        foreach (var candidate in candidates)
        {
            if (candidate.OfferedCopyId is not { } bookCopyId)
            {
                continue;
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var copy = await bookCopies.GetByIdForUpdateAsync(new BookCopyId(bookCopyId), cancellationToken).ConfigureAwait(false);
            if (copy is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var offered = await reservations.GetOfferedByCopyForUpdateAsync(bookCopyId, cancellationToken).ConfigureAwait(false);
            if (offered is null || offered.Id.Value != candidate.Id.Value || offered.ClaimWindowExpiresAt > now)
            {
                // Already claimed (or superseded) by a concurrent writer since the unlocked candidate
                // read above - not an error, just a no-op (mirrors GracePeriodExpiryService's own posture).
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var expired = offered.Expire(bookCopyId, now);
            if (expired.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var nextClaimWindow = now.AddHours(options.ReservationClaimWindowHours);
            var offeredNextId = await reservations.TryOfferNextQueuedAsync(copy.BookId.Value, bookCopyId, now, nextClaimWindow, cancellationToken).ConfigureAwait(false);
            if (offeredNextId is { } nextReservationId)
            {
                var next = await reservations.GetByIdAsync(new Domain.Reservations.ReservationId(nextReservationId), cancellationToken).ConfigureAwait(false);
                if (next is not null)
                {
                    domainEventRecorder.Enqueue(new ReservationFulfilled(nextReservationId, copy.BookId.Value, bookCopyId, next.BorrowerId, nextClaimWindow, now));
                }
            }
            else
            {
                copy.ReleaseReservationHold();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            expiredCount++;
        }

        return expiredCount;
    }
}
