namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// IDN-17/design-decisions.md, "Rate-Limiting / Lockout Mechanism": the ephemeral, per-identifier
/// half of account lockout - a Redis-backed atomic counter (edge-cases.md, "Concurrent login
/// attempts triggering lockout vs. legitimate retry race": "atomic counter increment... with the
/// lockout threshold compared only after the increment commits"). <see cref="Domain.Users.User.LockedOutAt"/>
/// is the durable half this counter feeds.
/// </summary>
public interface IFailedLoginAttemptTracker
{
    /// <summary>Atomically increments the identifier's failure counter and returns the new count.</summary>
    public Task<int> RegisterFailureAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>Clears the counter - called on a successful login so an occasional legitimate typo never accumulates toward lockout.</summary>
    public Task ResetAsync(string identifier, CancellationToken cancellationToken = default);
}
