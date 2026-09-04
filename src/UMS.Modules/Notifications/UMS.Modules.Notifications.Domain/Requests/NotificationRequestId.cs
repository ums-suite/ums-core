namespace UMS.Modules.Notifications.Domain.Requests;

public readonly record struct NotificationRequestId(Guid Value)
{
    public static NotificationRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
