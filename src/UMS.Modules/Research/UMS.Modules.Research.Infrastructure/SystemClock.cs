using UMS.Modules.Research.Application.Abstractions;

namespace UMS.Modules.Research.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
