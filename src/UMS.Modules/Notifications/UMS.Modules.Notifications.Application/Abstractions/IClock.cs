namespace UMS.Modules.Notifications.Application.Abstractions;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
