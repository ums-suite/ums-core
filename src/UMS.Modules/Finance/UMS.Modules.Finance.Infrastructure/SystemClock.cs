using UMS.Modules.Finance.Application.Abstractions;

namespace UMS.Modules.Finance.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
