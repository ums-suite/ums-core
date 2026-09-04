using UMS.Modules.Notifications.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Notifications.Infrastructure.Authorization;

/// <summary>Notifications' own contribution to the platform-wide Permission catalog (mirrors Audit's own <c>AuditPermissionManifest</c>).</summary>
internal sealed class NotificationsPermissionManifest : IPermissionManifest
{
    public string OwningModule => "notifications";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(NotificationsPermissions.TemplateManage, "List and manage Notification templates and their translations."),
        new(NotificationsPermissions.RequestRead, "Look up a NotificationRequest's delivery status by id, for cross-module support."),
        new(NotificationsPermissions.DeadLetterRead, "View the dead-letter triage queue."),
    ];
}
