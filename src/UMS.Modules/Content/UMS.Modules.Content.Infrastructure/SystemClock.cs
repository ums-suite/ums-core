using UMS.Modules.Content.Application.Abstractions;

namespace UMS.Modules.Content.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
