using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
