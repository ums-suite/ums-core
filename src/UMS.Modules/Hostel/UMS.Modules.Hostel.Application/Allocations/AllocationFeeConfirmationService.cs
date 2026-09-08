using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.Allocations;

/// <summary>
/// HOS-9: Finance's own <c>PaymentSucceeded</c>/<c>PaymentFailed</c> domain events, filtered to
/// <c>SourceModule == "hostel"</c> (requirement-spec.md §3's naming-reconciliation note: the spec's
/// generic "PaymentCompleted" term maps to these two actual events - the same reconciliation
/// Admission's own <c>ApplicationPaymentConfirmationRelayWorker</c> already had to make). Called by
/// <c>HostelFinancePaymentRelayWorker</c> for every unprocessed event in Finance's outbox.
///
/// <para>
/// Also owns the HOS-10 "late success" recovery path: edge-cases.md "Fee-payment grace period
/// expiring while payment is in flight" - a <c>PaymentSucceeded</c> arriving for an Allocation the
/// grace-period sweep has ALREADY expired triggers a fresh allocation attempt against the bed pool
/// for the same Student, falling back to an officer-notified manual-reconciliation flag (reusing
/// HOS-17's own additive <see cref="AllocationReviewFlag"/> side-table - both are advisory,
/// human-facing prompts, not state-machine gates) plus the same refund-request-via-domain-event
/// posture <see cref="AllocationService.CheckOutAsync"/> uses, if no bed remains.
/// </para>
/// </summary>
public sealed class AllocationFeeConfirmationService(
    IAllocationRepository allocationRepository,
    IHostelApplicationRepository applications,
    IAllocationReviewFlagRepository reviewFlags,
    AllocationService allocationService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <returns><see langword="true"/> if this event belonged to a Hostel Allocation (processed or intentionally ignored); <see langword="false"/> if it matches no known Allocation (a different module's own Finance usage).</returns>
    public async Task<bool> ApplyAsync(Guid invoiceId, string eventType, CancellationToken cancellationToken = default)
    {
        var allocation = await allocationRepository.GetByInvoiceIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (allocation is null)
        {
            return false;
        }

        if (!string.Equals(eventType, "PaymentSucceeded", StringComparison.Ordinal))
        {
            // PaymentFailed: no state change - the Student remains Pending and may retry payment
            // until the grace-period sweep (HOS-10) eventually reverts it.
            return true;
        }

        if (allocation.Status == AllocationStatus.Expired)
        {
            await HandleLatePaymentForExpiredAllocationAsync(allocation, cancellationToken).ConfigureAwait(false);
            return true;
        }

        var marked = allocation.MarkFeePaid(clock.UtcNow);
        if (marked.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task HandleLatePaymentForExpiredAllocationAsync(Allocation expiredAllocation, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(new HostelApplicationId(expiredAllocation.HostelApplicationId), cancellationToken).ConfigureAwait(false);
        var preferences = application?.Preferences.OrderBy(p => p.Rank).Select(p => (p.HostelId, p.PreferredRoomType)) ?? [];

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var bedSearch = await allocationService.FindAndLockAvailableBedAsync(preferences, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        if (bedSearch is { } found)
        {
            // FeeGraceDeadline is set to `now` (not the configured grace period) since this
            // Allocation is created already-FeePaid a few lines below - the grace-period sweep
            // (HOS-10) only ever considers Pending Allocations, so the field is inert here.
            var created = Allocation.Create(expiredAllocation.StudentId, found.BedId, found.RoomId, found.HostelId, expiredAllocation.HostelApplicationId, now, now);
            if (created.IsSuccess)
            {
                var fresh = created.Value;
                fresh.RecordInvoice(expiredAllocation.InvoiceId ?? Guid.Empty);
                fresh.MarkFeePaid(now);
                allocationRepository.Add(fresh);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        // No bed remains: officer-notified manual reconciliation + a refund request to Finance
        // (edge-cases.md) - modeled as the same additive, advisory AllocationReviewFlag row HOS-17
        // uses, rather than a second new mechanism.
        var flag = AllocationReviewFlag.Create(
            expiredAllocation.Id.Value,
            "Late PaymentSucceeded arrived for an already-expired Allocation and no replacement Bed is available - manual reconciliation and a Finance refund are required.",
            "PaymentSucceeded",
            now);
        if (flag.IsSuccess)
        {
            reviewFlags.Add(flag.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
