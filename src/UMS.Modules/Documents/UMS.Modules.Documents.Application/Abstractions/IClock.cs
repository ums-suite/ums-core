namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>Testability seam for "now" - mirrors Audit's/Identity's own local <c>IClock</c> (ums-conventions.md leaves this a per-module concern).</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
