using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Application.Abstractions;

public interface IChannelSuppressionRepository
{
    public void Add(ChannelSuppression suppression);

    public Task<bool> IsSuppressedAsync(Guid recipientId, NotificationChannel channel, CancellationToken cancellationToken = default);
}
