namespace UMS.Modules.Notifications.Domain.Requests;

public readonly record struct NotificationDeliveryAttemptId(Guid Value)
{
    public static NotificationDeliveryAttemptId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
