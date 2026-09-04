using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Api.Contracts;

public sealed record CreateTemplateRequestBody(string EventType, NotificationChannel Channel);
