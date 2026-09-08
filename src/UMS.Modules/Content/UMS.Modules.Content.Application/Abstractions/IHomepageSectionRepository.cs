using UMS.Modules.Content.Domain.HomepageSections;

namespace UMS.Modules.Content.Application.Abstractions;

public interface IHomepageSectionRepository
{
    public Task<HomepageSection?> GetByIdAsync(HomepageSectionId id, CancellationToken cancellationToken = default);

    public void Add(HomepageSection section);

    public Task<IReadOnlyList<HomepageSection>> ListEnabledOrderedAsync(CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<HomepageSection>> ListAllOrderedAsync(CancellationToken cancellationToken = default);
}
