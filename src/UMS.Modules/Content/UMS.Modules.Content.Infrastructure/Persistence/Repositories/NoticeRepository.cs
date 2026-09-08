using Microsoft.EntityFrameworkCore;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Notices;

namespace UMS.Modules.Content.Infrastructure.Persistence.Repositories;

internal sealed class NoticeRepository(ContentDbContext context) : INoticeRepository
{
    public Task<Notice?> GetByIdAsync(NoticeId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public void Add(Notice notice) => context.Notices.Add(notice);

    public async Task<IReadOnlyList<Notice>> ListAsync(ContentAudience? audience, SchedulableStatus? status, int skip, int take, CancellationToken cancellationToken = default) =>
        await Filter(Query(), audience, status)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(ContentAudience? audience, SchedulableStatus? status, CancellationToken cancellationToken = default) =>
        Filter(context.Notices, audience, status).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Notice>> GetDueForPublishAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(n => n.Status == SchedulableStatus.Scheduled && n.PublishAt != null && n.PublishAt <= now)
            .OrderBy(n => n.PublishAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Notice>> GetDueForExpireAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(n => n.Status == SchedulableStatus.Published && n.ExpireAt != null && n.ExpireAt <= now)
            .OrderBy(n => n.ExpireAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static IQueryable<Notice> Filter(IQueryable<Notice> query, ContentAudience? audience, SchedulableStatus? status)
    {
        if (audience is { } a)
        {
            query = query.Where(n => (n.Audience & a) != ContentAudience.None);
        }

        if (status is { } s)
        {
            query = query.Where(n => n.Status == s);
        }

        return query;
    }

    private IQueryable<Notice> Query() => context.Notices.Include(n => n.Translations);
}
