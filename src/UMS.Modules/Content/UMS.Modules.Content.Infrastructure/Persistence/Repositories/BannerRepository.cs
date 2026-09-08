using Microsoft.EntityFrameworkCore;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Banners;
using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.Infrastructure.Persistence.Repositories;

internal sealed class BannerRepository(ContentDbContext context) : IBannerRepository
{
    public Task<Banner?> GetByIdAsync(BannerId id, CancellationToken cancellationToken = default) =>
        context.Banners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Add(Banner banner) => context.Banners.Add(banner);

    public async Task<IReadOnlyList<Banner>> ListActiveOrderedAsync(CancellationToken cancellationToken = default) =>
        await context.Banners
            .Where(b => b.Status == SchedulableStatus.Published)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Banner>> ListAllOrderedAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Banners
            .OrderBy(b => b.SortOrder).ThenBy(b => b.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default) => context.Banners.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Banner>> GetDueForPublishAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Banners
            .Where(b => b.Status == SchedulableStatus.Scheduled && b.PublishAt != null && b.PublishAt <= now)
            .OrderBy(b => b.PublishAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Banner>> GetDueForExpireAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Banners
            .Where(b => b.Status == SchedulableStatus.Published && b.ExpireAt != null && b.ExpireAt <= now)
            .OrderBy(b => b.ExpireAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
