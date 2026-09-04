namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>Testability seam for "now" - every application service reads time through this instead of <see cref="DateTimeOffset.UtcNow"/> directly.</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
