using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Application.Allocations;

/// <summary>
/// HOS-10: the fee-payment grace-period auto-expiry scheduled job (requirement-spec.md §9 decision 3;
/// edge-cases.md "Fee-payment grace period expiring while payment is in flight"). A TTL-based
/// periodic sweep, fully self-contained inside Hostel - no synchronous query-back to Finance, no
/// pause/extend signal (design-decisions.md "Fee-Grace-Period Expiry Mechanism").
///
/// <para>
/// Each candidate Allocation is expired in its own transaction, under the SAME <c>SELECT ... FOR
/// UPDATE</c> Bed-row lock discipline as allocation-creation and check-out
/// (design-decisions.md "Bed-Allocation Concurrency Control Pattern": "applied uniformly to every
/// writer that changes a Bed's occupancy state") - expiry frees the Bed exactly as check-out does,
/// so it gets the same lock, not a lighter-weight one.
/// </para>
/// </summary>
public sealed class GracePeriodExpiryService(
    IAllocationRepository allocations,
    IBedRepository beds,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<int> SweepAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var candidates = await allocations.GetPendingPastGraceDeadlineAsync(now, batchSize, cancellationToken).ConfigureAwait(false);
        var expiredCount = 0;

        foreach (var candidate in candidates)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var lockedBed = await beds.GetByIdForUpdateAsync(new BedId(candidate.BedId), cancellationToken).ConfigureAwait(false);
            if (lockedBed is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var expired = candidate.Expire(now);
            if (expired.IsFailure)
            {
                // Already transitioned away from Pending by a concurrent writer (e.g. a late
                // PaymentSucceeded that raced this exact sweep pass) - not an error, just a no-op.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            expiredCount++;
        }

        return expiredCount;
    }
}
