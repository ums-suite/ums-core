using UMS.Modules.Career.Application.Abstractions;

namespace UMS.Modules.Career.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
