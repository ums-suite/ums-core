using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.MeritLists;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

internal sealed class MeritListRepository(AdmissionDbContext context) : IMeritListRepository
{
    public Task<MeritList?> GetByIdAsync(MeritListId id, CancellationToken cancellationToken = default) =>
        context.MeritLists.Include(m => m.Entries).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<MeritList?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        context.MeritLists.Include(m => m.Entries).FirstOrDefaultAsync(m => m.CampaignId == campaignId, cancellationToken);

    public void Add(MeritList meritList) => context.MeritLists.Add(meritList);
}
