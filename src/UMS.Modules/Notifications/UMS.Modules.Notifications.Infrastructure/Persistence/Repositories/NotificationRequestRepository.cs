using Microsoft.EntityFrameworkCore;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRequestRepository(NotificationsDbContext context) : INotificationRequestRepository
{
    public void Add(NotificationRequest request) => context.NotificationRequests.Add(request);

    public async Task<NotificationRequest?> GetByIdAsync(NotificationRequestId id, CancellationToken cancellationToken = default) =>
        await context.NotificationRequests
            .Include(r => r.Attempts)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotificationRequest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var typedIds = ids.Select(id => new NotificationRequestId(id)).ToHashSet();
        return await context.NotificationRequests
            .Where(r => typedIds.Contains(r.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
