namespace UMS.Shared.Authorization;

/// <summary>
/// The claim names Identity's token issuer writes and every protected endpoint (in every module)
/// reads (requirement-spec.md identity §2: "JWT claims carry `sub` ... `roles` ... and `sid`").
/// Kept as raw strings matching the JWT wire format exactly - <see cref="DependencyInjection"/>
/// disables ASP.NET Core's default inbound claim-type remapping so these never get rewritten to
/// the legacy `http://schemas.xmlsoap.org/...` URIs.
/// </summary>
public static class UmsClaimTypes
{
    public const string Subject = "sub";
    public const string SessionId = "sid";
    public const string Roles = "roles";

    /// <summary>
    /// Identity's IDN-11 mid-login MFA-challenge token's own distinguishing claim (present, valued
    /// "1") - carries no <see cref="SessionId"/>, so it authenticates via the platform's shared JWT
    /// middleware like any other token but is deliberately rejected by <c>RequireLiveSession()</c>.
    /// Declared here, not in Identity's own module, because it is a wire-format detail the shared
    /// authentication pipeline validates the same way for every module, mirroring why <see cref="Subject"/>/
    /// <see cref="SessionId"/>/<see cref="Roles"/> already live here rather than in Identity's own assembly.
    /// </summary>
    public const string MfaChallenge = "mfa_challenge";
}
