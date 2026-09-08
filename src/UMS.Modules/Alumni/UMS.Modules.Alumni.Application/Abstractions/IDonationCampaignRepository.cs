using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IDonationCampaignRepository
{
    public Task<DonationCampaign?> GetByIdAsync(DonationCampaignId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DonationCampaign>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    public void Add(DonationCampaign campaign);
}
