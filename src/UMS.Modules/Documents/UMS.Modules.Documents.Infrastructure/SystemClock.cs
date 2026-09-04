using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
