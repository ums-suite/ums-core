namespace UMS.Modules.Admission.Application.Abstractions;

/// <summary>The server's own clock - never <c>DateTimeOffset.UtcNow</c> read inline (requirement-spec.md §5: "the server clock is authoritative, never the client's"). Mirrors every other module's own <c>IClock</c> exactly.</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
