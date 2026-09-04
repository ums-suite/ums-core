namespace UMS.Modules.Notifications.Application.Permissions;

/// <summary>requirement-spec.md notifications §6 API surface - one Permission per protected admin/ops endpoint group (mirrors Audit's own <c>AuditPermissions</c>).</summary>
public static class NotificationsPermissions
{
    public const string TemplateManage = "notifications.template.manage";

    public const string RequestRead = "notifications.request.read";

    public const string DeadLetterRead = "notifications.deadletter.read";
}
