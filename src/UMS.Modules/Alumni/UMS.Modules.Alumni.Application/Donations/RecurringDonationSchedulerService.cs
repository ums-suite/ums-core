using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Donations;
using UMS.Shared.Finance;

namespace UMS.Modules.Alumni.Application.Donations;

/// <summary>
/// ALM-10: Alumni's own recurring-donation scheduler (requirement-spec.md §2.4) - triggers a new
/// Finance Payment request for every recurring series' root Donation that is due for its next cycle.
/// design-decisions.md's campaign-window decision (item 7) is deliberately NOT re-applied here: the
/// campaign was already validated active at the SERIES ROOT's own creation time, and a recurring
/// pledge is not re-litigated against the campaign's window on every subsequent cycle (the same
/// "never second-guess an already-accepted donation for a window reason" posture, extended one step
/// further for an ongoing pledge rather than re-derived from scratch).
/// </summary>
public sealed class RecurringDonationSchedulerService(
    IDonationRepository donations,
    IInvoiceRequester invoiceRequester,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<int> TriggerDueCyclesAsync(int batchSize, string correlationId, CancellationToken cancellationToken = default)
    {
        var due = await donations.ListRecurringDueAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var triggeredCount = 0;

        foreach (var root in due)
        {
            var cycle = Donation.CreateNextCycle(root, clock.UtcNow);

            var invoice = await invoiceRequester.CreateInvoiceAsync(
                new CreateInvoiceCommand("alumni", cycle.Id.Value.ToString(), "Donation", cycle.OwnerUserId, ApplicabilityReferenceId: null, RequestedByUserId: null, correlationId),
                cancellationToken).ConfigureAwait(false);
            if (invoice.IsFailure)
            {
                continue;
            }

            cycle.RecordInvoice(invoice.Value.InvoiceId);
            donations.Add(cycle);

            // Advances the root's own NextChargeAt so an unchanged root is not re-picked up next poll
            // - decoupled from this new cycle's own eventual Confirm/Fail outcome (a genuine failure
            // pauses the schedule separately, once Finance's signal arrives - see
            // DonationConfirmationService).
            root.AdvanceScheduleAfterTriggering(clock.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            triggeredCount++;
        }

        return triggeredCount;
    }
}
