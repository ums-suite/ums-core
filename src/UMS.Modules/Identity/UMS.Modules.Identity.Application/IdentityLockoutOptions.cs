namespace UMS.Modules.Identity.Application;

/// <summary>IDN-17 tuning (bound from <c>Identity:Lockout</c>) - design-decisions.md, "Rate-Limiting / Lockout Mechanism".</summary>
public sealed class IdentityLockoutOptions
{
    /// <summary>requirement-spec.md identity §2 "a configurable threshold" - the identifier-dimension count that flips <see cref="Domain.Users.User.LockedOutAt"/>.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>The Redis counter's own window - a failure older than this no longer counts toward the threshold.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(15);
}
