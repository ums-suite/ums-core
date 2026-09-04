using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Workers.Notifications;

/// <summary>§4 invariant: "In-app is treated as Notifications' own 'must succeed' channel" - still dispatched through the same claim/process pipeline as every other channel (no external provider call, but the same retry-state bookkeeping and per-channel independence).</summary>
internal sealed class InAppDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<InAppDispatchWorker> logger)
    : NotificationChannelDispatchWorkerBase(NotificationChannel.InApp, scopeFactory, logger);
