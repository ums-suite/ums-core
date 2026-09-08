using Microsoft.EntityFrameworkCore;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Downloads;

namespace UMS.Modules.Content.Infrastructure.Persistence.Repositories;

internal sealed class DownloadResourceRepository(ContentDbContext context) : IDownloadResourceRepository
{
    public Task<DownloadResource?> GetByIdAsync(DownloadResourceId id, CancellationToken cancellationToken = default) =>
        context.DownloadResources.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Add(DownloadResource resource) => context.DownloadResources.Add(resource);

    public async Task<IReadOnlyList<DownloadResource>> ListPublishedAsync(string? category, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.DownloadResources.Where(d => d.Status == SchedulableStatus.Published);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(d => d.Category == category);
        }

        return await query.OrderByDescending(d => d.CreatedAt).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountPublishedAsync(string? category, CancellationToken cancellationToken = default)
    {
        var query = context.DownloadResources.Where(d => d.Status == SchedulableStatus.Published);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(d => d.Category == category);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadResource>> ListAllAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.DownloadResources.OrderByDescending(d => d.CreatedAt).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<DownloadResource>> GetDueForPublishAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.DownloadResources
            .Where(d => d.Status == SchedulableStatus.Scheduled && d.PublishAt != null && d.PublishAt <= now)
            .OrderBy(d => d.PublishAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<DownloadResource>> GetDueForExpireAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.DownloadResources
            .Where(d => d.Status == SchedulableStatus.Published && d.ExpireAt != null && d.ExpireAt <= now)
            .OrderBy(d => d.ExpireAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
