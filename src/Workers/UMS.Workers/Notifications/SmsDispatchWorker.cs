using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Workers.Notifications;

internal sealed class SmsDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<SmsDispatchWorker> logger)
    : NotificationChannelDispatchWorkerBase(NotificationChannel.Sms, scopeFactory, logger);
