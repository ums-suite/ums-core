namespace UMS.Modules.Identity.Application;

/// <summary>
/// IDN-10/11 tuning (bound from <c>Identity:Mfa</c>, mirroring <see cref="IdentityTokenOptions"/>'s
/// own configuration-not-hardcoded pattern). <see cref="MasterKeyBase64"/> is design-decisions.md's
/// "MFA Secret Storage" envelope-encryption master key - a base64-encoded 32-byte AES-256 key.
/// </summary>
public sealed class IdentityMfaOptions
{
    /// <summary>edge-cases.md, "MFA enrollment interrupted mid-flow": "a 10-minute TTL" on a pending, unverified secret.</summary>
    public TimeSpan PendingSecretLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long the interim token issued mid-login (when MFA is required) remains presentable to <c>POST /auth/mfa/verify</c>.</summary>
    public TimeSpan ChallengeTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The issuer label an authenticator app displays next to the account (otpauth:// URI's own <c>issuer</c> parameter).</summary>
    public string Issuer { get; set; } = "UMS";

    public string MasterKeyBase64 { get; set; } = string.Empty;
}
