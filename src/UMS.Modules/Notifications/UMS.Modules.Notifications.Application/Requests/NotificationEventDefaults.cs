using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Application.Requests;

public sealed record NotificationEventDefaults(NotificationCategory Category, NotificationPriority Priority, IReadOnlyCollection<NotificationChannel> Channels);
