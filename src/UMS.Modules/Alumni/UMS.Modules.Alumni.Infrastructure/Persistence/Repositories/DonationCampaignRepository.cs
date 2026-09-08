using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Donations;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class DonationCampaignRepository(AlumniDbContext context) : IDonationCampaignRepository
{
    public Task<DonationCampaign?> GetByIdAsync(DonationCampaignId id, CancellationToken cancellationToken = default) =>
        context.DonationCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DonationCampaign>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.DonationCampaigns.OrderByDescending(c => c.StartsAt).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(DonationCampaign campaign) => context.DonationCampaigns.Add(campaign);
}
