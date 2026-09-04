using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Workers.Notifications;

internal sealed class EmailDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<EmailDispatchWorker> logger)
    : NotificationChannelDispatchWorkerBase(NotificationChannel.Email, scopeFactory, logger);
