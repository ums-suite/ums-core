namespace UMS.Modules.Notifications.Domain.Preferences;

public readonly record struct RecipientNotificationPreferenceId(Guid Value)
{
    public static RecipientNotificationPreferenceId New() => new(Guid.NewGuid());
}
