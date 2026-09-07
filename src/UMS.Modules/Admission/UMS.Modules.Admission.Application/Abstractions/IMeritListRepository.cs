using UMS.Modules.Admission.Domain.MeritLists;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IMeritListRepository
{
    public Task<MeritList?> GetByIdAsync(MeritListId id, CancellationToken cancellationToken = default);

    public Task<MeritList?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default);

    public void Add(MeritList meritList);
}
