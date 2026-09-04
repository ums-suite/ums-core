using UMS.Modules.Identity.Application.Abstractions;

namespace UMS.Modules.Identity.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
