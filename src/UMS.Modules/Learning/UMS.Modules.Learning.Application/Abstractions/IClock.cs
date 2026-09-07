namespace UMS.Modules.Learning.Application.Abstractions;

/// <summary>
/// The server's own clock - the single source of <c>Submission.submittedAt</c>
/// (requirement-spec.md learning §4's server-authoritative invariant). Injected rather than read
/// from <c>DateTimeOffset.UtcNow</c> inline so the window-boundary invariants are directly testable
/// at every tier boundary without waiting real time.
/// </summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
