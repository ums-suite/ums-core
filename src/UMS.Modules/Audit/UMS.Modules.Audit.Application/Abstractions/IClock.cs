namespace UMS.Modules.Audit.Application.Abstractions;

/// <summary>Testability seam for "now" - mirrors Identity's own local <c>IClock</c> (each module owns its own, ums-conventions.md leaves this a per-module concern).</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
