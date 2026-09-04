namespace UMS.Modules.Notifications.Domain.Preferences;

public readonly record struct ChannelSuppressionId(Guid Value)
{
    public static ChannelSuppressionId New() => new(Guid.NewGuid());
}
