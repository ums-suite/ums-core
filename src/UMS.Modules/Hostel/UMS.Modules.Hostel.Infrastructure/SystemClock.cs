using UMS.Modules.Hostel.Application.Abstractions;

namespace UMS.Modules.Hostel.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
