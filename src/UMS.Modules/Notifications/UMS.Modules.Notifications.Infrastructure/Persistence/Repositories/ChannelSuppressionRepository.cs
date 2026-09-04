using Microsoft.EntityFrameworkCore;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class ChannelSuppressionRepository(NotificationsDbContext context) : IChannelSuppressionRepository
{
    public void Add(ChannelSuppression suppression) => context.ChannelSuppressions.Add(suppression);

    public Task<bool> IsSuppressedAsync(Guid recipientId, NotificationChannel channel, CancellationToken cancellationToken = default) =>
        context.ChannelSuppressions.AnyAsync(s => s.RecipientId == recipientId && s.Channel == channel, cancellationToken);
}
