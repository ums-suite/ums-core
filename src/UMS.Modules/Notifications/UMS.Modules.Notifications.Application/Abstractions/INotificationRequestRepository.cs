using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Application.Abstractions;

public interface INotificationRequestRepository
{
    public void Add(NotificationRequest request);

    public Task<NotificationRequest?> GetByIdAsync(NotificationRequestId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<NotificationRequest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
