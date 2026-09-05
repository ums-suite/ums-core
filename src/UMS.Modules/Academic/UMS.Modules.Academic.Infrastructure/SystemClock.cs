using UMS.Modules.Academic.Application.Abstractions;

namespace UMS.Modules.Academic.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
