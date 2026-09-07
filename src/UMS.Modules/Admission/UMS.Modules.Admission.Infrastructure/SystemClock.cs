using UMS.Modules.Admission.Application.Abstractions;

namespace UMS.Modules.Admission.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
