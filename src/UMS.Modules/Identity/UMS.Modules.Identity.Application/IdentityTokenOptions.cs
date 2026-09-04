namespace UMS.Modules.Identity.Application;

/// <summary>
/// The fixed v1 token-lifetime decision (requirement-spec.md identity §9.1: "Access-token TTL
/// fixed at 15 minutes, refresh at 14 days ... revisit under real usage data") plus the refresh
/// reuse grace window (edge-cases.md, "Same-device concurrent refresh (two tabs)"). Bound from
/// configuration (<c>Identity:Tokens</c>) rather than hardcoded so a future tuning pass is a
/// config change, not a code change.
/// </summary>
public sealed class IdentityTokenOptions
{
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    public TimeSpan RefreshReuseGraceWindow { get; set; } = TimeSpan.FromSeconds(5);
}
