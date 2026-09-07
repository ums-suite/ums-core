using UMS.Modules.Admission.Domain.Campaigns;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface ICampaignRepository
{
    public Task<AdmissionCampaign?> GetByIdAsync(AdmissionCampaignId id, CancellationToken cancellationToken = default);

    public void Add(AdmissionCampaign campaign);
}
