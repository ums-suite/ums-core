using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Donations;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Donations;

/// <summary>ALM-8: DonationCampaign reference data (requirement-spec.md §2.4).</summary>
public sealed class DonationCampaignService(IDonationCampaignRepository campaigns, IUnitOfWork unitOfWork, IClock clock)
{
    public static DonationCampaignDto ToDto(DonationCampaign campaign) => new(
        campaign.Id.Value, campaign.Name, campaign.Description, campaign.GoalAmount, campaign.Currency, campaign.StartsAt, campaign.EndsAt, campaign.ClosedEarly, campaign.CreatedAt);

    public async Task<Result<DonationCampaignDto>> CreateAsync(CreateDonationCampaignRequest request, CancellationToken cancellationToken = default)
    {
        DonationCampaign campaign;
        try
        {
            campaign = DonationCampaign.Create(request.Name, request.Description, request.GoalAmount, request.Currency, request.StartsAt, request.EndsAt, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("donationcampaign.invalid", ex.Message);
        }

        campaigns.Add(campaign);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(campaign);
    }

    public async Task<IReadOnlyList<DonationCampaignDto>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await campaigns.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }

    public async Task<Result<DonationCampaignDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new DonationCampaignId(id), cancellationToken).ConfigureAwait(false);
        return campaign is null ? Error.NotFound("donationcampaign.not_found", $"No DonationCampaign exists with id '{id}'.") : ToDto(campaign);
    }

    public async Task<Result<DonationCampaignDto>> CloseEarlyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(new DonationCampaignId(id), cancellationToken).ConfigureAwait(false);
        if (campaign is null)
        {
            return Error.NotFound("donationcampaign.not_found", $"No DonationCampaign exists with id '{id}'.");
        }

        campaign.CloseEarly();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(campaign);
    }
}
