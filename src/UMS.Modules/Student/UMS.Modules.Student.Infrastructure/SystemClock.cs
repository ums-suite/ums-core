using UMS.Modules.Student.Application.Abstractions;

namespace UMS.Modules.Student.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
