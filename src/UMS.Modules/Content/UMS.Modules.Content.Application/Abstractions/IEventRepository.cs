using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;

namespace UMS.Modules.Content.Application.Abstractions;

public interface IEventRepository
{
    public Task<Event?> GetByIdAsync(EventId id, CancellationToken cancellationToken = default);

    public void Add(Event calendarEvent);

    /// <summary>CNT-7: calendar-style listing, filterable by date range/audience/Organization node.</summary>
    public Task<IReadOnlyList<Event>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, ContentAudience? audience, Guid? organizationNodeId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(DateTimeOffset? from, DateTimeOffset? to, ContentAudience? audience, Guid? organizationNodeId, CancellationToken cancellationToken = default);
}
