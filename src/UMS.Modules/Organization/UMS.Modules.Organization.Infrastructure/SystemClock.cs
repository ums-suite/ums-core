using UMS.Modules.Organization.Application.Abstractions;

namespace UMS.Modules.Organization.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
