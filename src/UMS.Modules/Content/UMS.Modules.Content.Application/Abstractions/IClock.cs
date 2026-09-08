namespace UMS.Modules.Content.Application.Abstractions;

/// <summary>The server's own clock - never <c>DateTimeOffset.UtcNow</c> read inline. Mirrors every other module's own <c>IClock</c> exactly.</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
