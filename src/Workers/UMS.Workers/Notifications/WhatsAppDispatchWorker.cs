using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Workers.Notifications;

internal sealed class WhatsAppDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<WhatsAppDispatchWorker> logger)
    : NotificationChannelDispatchWorkerBase(NotificationChannel.WhatsApp, scopeFactory, logger);
