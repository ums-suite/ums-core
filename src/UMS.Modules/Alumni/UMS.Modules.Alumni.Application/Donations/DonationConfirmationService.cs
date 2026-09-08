using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Common;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Donations;

/// <summary>
/// ALM-9: consumes Finance's PaymentSucceeded/PaymentFailed for donation-tagged invoices
/// (requirement-spec.md §2.4, §4, §5). design-decisions.md "Donation Confirmation Consistency
/// Model": this is the ONLY path to <c>Confirmed</c> - never the donor's redirect return.
/// </summary>
public sealed class DonationConfirmationService(
    IDonationRepository donations,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IAlumniNotificationPublisher notifications,
    IClock clock)
{
    /// <summary>Returns <see langword="true"/> if this event matched one of Alumni's own Donations (regardless of outcome) - the relay marks EVERY event processed either way (an unmatched event belongs to a different module's own Finance usage).</summary>
    public async Task<Result<bool>> ApplyAsync(Guid invoiceId, string eventType, string correlationId, CancellationToken cancellationToken = default)
    {
        var donation = await donations.GetByInvoiceIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (donation is null)
        {
            return false;
        }

        if (eventType == "PaymentSucceeded")
        {
            // requirement-spec.md §5 Auditability: "Donation confirmation ... requires an
            // AuditLogEntry" - the one Donation mutation this ticket set names as sensitive enough
            // to audit synchronously.
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            donation.Confirm(clock.UtcNow);
            var audit = AuditContext.ForSystemJob("alumni-finance-payment-relay", correlationId, "Donation", donation.Id.Value.ToString(), AuditActions.Update, "{\"status\":\"Pending\"}", "{\"status\":\"Confirmed\"}");
            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
            return committed.IsFailure ? committed.Error! : true;
        }

        if (eventType == "PaymentFailed")
        {
            // design-decisions.md "Recurring-Donation Retry/Dunning Policy": pause on first failure,
            // notify the donor, never retry this cycle automatically. A failing NON-root cycle
            // (2nd/3rd/... charge) cannot pause itself - Donation.Fail only mutates the row it is
            // called on - so the series ROOT is loaded separately and paused explicitly.
            var wasRecurring = donation.IsRecurring;
            var seriesRootId = donation.SeriesRootDonationId;
            var alumnusId = donation.AlumnusId;

            donation.Fail(clock.UtcNow);

            if (wasRecurring && seriesRootId is { } rootId)
            {
                var root = await donations.GetByIdAsync(rootId, cancellationToken).ConfigureAwait(false);
                root?.PauseRecurrence();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (wasRecurring)
            {
                await notifications.PublishAsync(
                    new AlumniNotificationRequest("DonationRecurrencePaused", donation.Id.Value.ToString(), alumnusId, new Dictionary<string, string?> { ["donationId"] = donation.Id.Value.ToString() }),
                    cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        return true;
    }
}
