using UMS.Modules.Content.Domain.Banners;
using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Application.Abstractions;

public interface IBannerRepository
{
    public Task<Banner?> GetByIdAsync(BannerId id, CancellationToken cancellationToken = default);

    public void Add(Banner banner);

    /// <summary>Public read: currently-Published banners, ordered per <see cref="ContentOrdering.ByDisplayOrder{T}(IEnumerable{T})"/>.</summary>
    public Task<IReadOnlyList<Banner>> ListActiveOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin listing - every status.</summary>
    public Task<IReadOnlyList<Banner>> ListAllOrderedAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Banner>> GetDueForPublishAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Banner>> GetDueForExpireAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
}
