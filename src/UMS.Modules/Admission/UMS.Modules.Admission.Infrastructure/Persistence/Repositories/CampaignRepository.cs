using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Campaigns;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

internal sealed class CampaignRepository(AdmissionDbContext context) : ICampaignRepository
{
    public Task<AdmissionCampaign?> GetByIdAsync(AdmissionCampaignId id, CancellationToken cancellationToken = default) =>
        context.Campaigns
            .Include(c => c.EligibilityRules)
            .Include(c => c.SeatQuotas)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(AdmissionCampaign campaign) => context.Campaigns.Add(campaign);
}
