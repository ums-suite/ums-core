using UMS.Modules.Notifications.Application.Abstractions;

namespace UMS.Modules.Notifications.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
