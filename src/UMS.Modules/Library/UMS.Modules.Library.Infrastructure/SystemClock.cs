using UMS.Modules.Library.Application.Abstractions;

namespace UMS.Modules.Library.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
