using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Donations;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;

namespace UMS.Modules.Alumni.Application.Donations;

/// <summary>
/// ALM-8/ALM-10: Donation initiation and donor-controlled recurrence management
/// (requirement-spec.md §2.4, §4, §6). Delegates 100% of payment processing to Finance
/// (<see cref="IInvoiceRequester"/>) - Alumni holds no gateway credentials, no PaymentTransaction
/// state of its own (requirement-spec.md item 9).
/// </summary>
public sealed class DonationService(
    IDonationRepository donations,
    IDonationCampaignRepository campaigns,
    IInvoiceRequester invoiceRequester,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public static DonationDto ToDto(Donation donation) => new(
        donation.Id.Value,
        donation.AlumnusId,
        donation.CampaignId,
        donation.Amount,
        donation.Currency,
        donation.IsAnonymous,
        donation.RecurrenceInterval.ToString(),
        donation.RecurrenceStatus?.ToString(),
        donation.SeriesRootDonationId?.Value,
        donation.Status.ToString(),
        donation.InvoiceId,
        donation.CreatedAt,
        donation.ConfirmedAt,
        donation.NextChargeAt,
        donation.Version);

    /// <summary>
    /// design-decisions.md "Donation-vs-Campaign-Close Consistency Boundary": the campaign's active
    /// window is checked HERE, at creation, and nowhere else - a Donation that clears this check
    /// proceeds to Confirmed strictly on Finance's own signal regardless of the campaign's state by
    /// then (see <see cref="DonationConfirmationService"/>, which never re-checks the campaign).
    /// </summary>
    public async Task<Result<DonationDto>> InitiateAsync(Guid alumnusId, Guid ownerUserId, InitiateDonationRequest request, string correlationId, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new DonationCampaignId(request.CampaignId), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("donationcampaign.not_found", $"No DonationCampaign exists with id '{request.CampaignId}'.");
        }

        var now = clock.UtcNow;
        if (!campaign.IsActive(now))
        {
            return Error.Conflict("donationcampaign.not_active", $"DonationCampaign '{request.CampaignId}' is not currently accepting donations.");
        }

        if (!Enum.TryParse<RecurrenceInterval>(request.RecurrenceInterval, ignoreCase: true, out var recurrenceInterval))
        {
            return Error.Validation("donation.invalid_recurrence_interval", $"'{request.RecurrenceInterval}' is not a recognized RecurrenceInterval.");
        }

        Donation donation;
        try
        {
            donation = Donation.Initiate(alumnusId, ownerUserId, request.CampaignId, request.Amount, request.Currency, request.IsAnonymous, recurrenceInterval, now);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("donation.invalid", ex.Message);
        }

        // requirement-spec.md item 9: the ONLY way a Donation ever touches money - a direct,
        // synchronous in-process call to Finance's own application-service interface.
        var invoice = await invoiceRequester.CreateInvoiceAsync(
            new CreateInvoiceCommand("alumni", donation.Id.Value.ToString(), "Donation", ownerUserId, ApplicabilityReferenceId: null, ownerUserId, correlationId),
            cancellationToken).ConfigureAwait(false);
        if (invoice.IsFailure)
        {
            return invoice.Error!;
        }

        donation.RecordInvoice(invoice.Value.InvoiceId);
        donations.Add(donation);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(donation);
    }

    public async Task<Result<DonationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var donation = await donations.GetByIdAsync(new DonationId(id), cancellationToken).ConfigureAwait(false);
        return donation is null ? Error.NotFound("donation.not_found", $"No Donation exists with id '{id}'.") : ToDto(donation);
    }

    public async Task<IReadOnlyList<DonationDto>> ListByAlumnusAsync(Guid alumnusId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await donations.ListByAlumnusAsync(alumnusId, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }

    /// <summary>requirement-spec.md §6 <c>POST /donations/{id}/cancel-recurring</c> - donor-initiated, any time. Ownership is enforced by the calling Api layer.</summary>
    public async Task<Result<DonationDto>> CancelRecurringAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var donation = await donations.GetByIdAsync(new DonationId(id), cancellationToken).ConfigureAwait(false);
        if (donation is null)
        {
            return Error.NotFound("donation.not_found", $"No Donation exists with id '{id}'.");
        }

        try
        {
            donation.CancelRecurring();
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("donation.not_recurring", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(donation);
    }

    /// <summary>edge-cases.md's own "resume path" sibling to cancel-recurring - explicit donor action required to resume a Paused schedule (requirement-spec.md §8).</summary>
    public async Task<Result<DonationDto>> ResumeRecurringAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var donation = await donations.GetByIdAsync(new DonationId(id), cancellationToken).ConfigureAwait(false);
        if (donation is null)
        {
            return Error.NotFound("donation.not_found", $"No Donation exists with id '{id}'.");
        }

        try
        {
            donation.ResumeRecurring(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("donation.not_paused", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(donation);
    }
}
