using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Downloads;

namespace UMS.Modules.Content.Application.Abstractions;

public interface IDownloadResourceRepository
{
    public Task<DownloadResource?> GetByIdAsync(DownloadResourceId id, CancellationToken cancellationToken = default);

    public void Add(DownloadResource resource);

    public Task<IReadOnlyList<DownloadResource>> ListPublishedAsync(string? category, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountPublishedAsync(string? category, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DownloadResource>> ListAllAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DownloadResource>> GetDueForPublishAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DownloadResource>> GetDueForExpireAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
}
