using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.UnitTests.Fakes;

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}
