using UMS.Modules.Audit.Application.Abstractions;

namespace UMS.Modules.Audit.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
