using UMS.Modules.Faculty.Application.Abstractions;

namespace UMS.Modules.Faculty.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
