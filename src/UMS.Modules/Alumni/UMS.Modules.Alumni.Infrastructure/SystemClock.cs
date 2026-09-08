using UMS.Modules.Alumni.Application.Abstractions;

namespace UMS.Modules.Alumni.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
