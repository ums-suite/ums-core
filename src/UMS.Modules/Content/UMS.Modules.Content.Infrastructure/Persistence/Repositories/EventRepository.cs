using Microsoft.EntityFrameworkCore;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;

namespace UMS.Modules.Content.Infrastructure.Persistence.Repositories;

internal sealed class EventRepository(ContentDbContext context) : IEventRepository
{
    public Task<Event?> GetByIdAsync(EventId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(Event calendarEvent) => context.Events.Add(calendarEvent);

    public async Task<IReadOnlyList<Event>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, ContentAudience? audience, Guid? organizationNodeId, int skip, int take, CancellationToken cancellationToken = default) =>
        await Filter(Query(), from, to, audience, organizationNodeId)
            .OrderBy(e => e.StartAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(DateTimeOffset? from, DateTimeOffset? to, ContentAudience? audience, Guid? organizationNodeId, CancellationToken cancellationToken = default) =>
        Filter(context.Events, from, to, audience, organizationNodeId).CountAsync(cancellationToken);

    private static IQueryable<Event> Filter(IQueryable<Event> query, DateTimeOffset? from, DateTimeOffset? to, ContentAudience? audience, Guid? organizationNodeId)
    {
        if (from is { } f)
        {
            query = query.Where(e => e.EndAt >= f);
        }

        if (to is { } t)
        {
            query = query.Where(e => e.StartAt <= t);
        }

        if (audience is { } a)
        {
            query = query.Where(e => (e.Audience & a) != ContentAudience.None);
        }

        if (organizationNodeId is { } nodeId)
        {
            query = query.Where(e => e.OrganizationNodeId == nodeId);
        }

        return query;
    }

    private IQueryable<Event> Query() => context.Events.Include(e => e.Translations);
}
