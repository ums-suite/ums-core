using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.Campaigns;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.Applications;

/// <summary>
/// design-decisions.md "Idempotency for Application Submission (Including Duplicate Payment-
/// Webhook Delivery)": the payment-confirmation-relay side of the SAME idempotent transition
/// <see cref="ApplicationService.SubmitAsync"/> uses. Driven by <c>UMS.Workers</c>' Admission relay
/// polling Finance's own outbox (<see cref="IFinancePaymentEventSource"/>).
/// </summary>
public sealed class ApplicationPaymentConfirmationService(
    IApplicationRepository applications,
    ICampaignRepository campaigns,
    ApplicationService applicationService)
{
    /// <summary>Returns <c>true</c> if this event matched one of Admission's own Applications (regardless of whether it changed anything) - the relay marks EVERY event processed either way (an unmatched event belongs to a different module's own Finance usage, per <see cref="IFinancePaymentEventSource"/>'s own remarks).</summary>
    public async Task<Result<bool>> ApplyAsync(Guid invoiceId, string eventType, string correlationId, CancellationToken cancellationToken = default)
    {
        var byApplicationFee = await applications.GetByApplicationFeeInvoiceIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (byApplicationFee is not null)
        {
            return await ApplyApplicationFeeEventAsync(byApplicationFee, eventType, correlationId, cancellationToken).ConfigureAwait(false);
        }

        var byConfirmationFee = await applications.GetByConfirmationFeeInvoiceIdAsync(invoiceId, cancellationToken).ConfigureAwait(false);
        if (byConfirmationFee is not null)
        {
            if (eventType == "PaymentSucceeded")
            {
                await applications.TryMarkConfirmationFeePaidAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        return false;
    }

    private async Task<Result<bool>> ApplyApplicationFeeEventAsync(Domain.Applications.Application application, string eventType, string correlationId, CancellationToken cancellationToken)
    {
        if (eventType != "PaymentSucceeded")
        {
            return true;
        }

        // Idempotent no-op on a duplicate delivery (edge-cases.md).
        await applications.TryMarkApplicationFeePaidAsync(application.ApplicationFeeInvoiceId!.Value, cancellationToken).ConfigureAwait(false);

        var reloaded = await applications.GetByIdAsync(application.Id, cancellationToken).ConfigureAwait(false);
        if (reloaded is null || reloaded.Status != ApplicationStatus.Draft)
        {
            return true;
        }

        var campaign = await campaigns.GetByIdAsync(new AdmissionCampaignId(reloaded.CampaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return true;
        }

        var submittable = reloaded.EnsureSubmittable(campaign.RequiredDocumentTypes);
        if (submittable.IsFailure)
        {
            // Fee is now recorded paid; the applicant's own explicit submit call will succeed once
            // any other gate condition (documents) is also satisfied - not this relay's job to wait.
            return true;
        }

        await applicationService.TryLockAndAuditAsync(reloaded, campaign, correlationId, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
