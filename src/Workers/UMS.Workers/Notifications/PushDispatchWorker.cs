using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Workers.Notifications;

internal sealed class PushDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<PushDispatchWorker> logger)
    : NotificationChannelDispatchWorkerBase(NotificationChannel.Push, scopeFactory, logger);
