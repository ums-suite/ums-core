namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>The server's own clock - never <c>DateTimeOffset.UtcNow</c> read inline, so every timing invariant (FeeStructure effective windows, stale-payment timeouts) is directly testable without waiting real time.</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
