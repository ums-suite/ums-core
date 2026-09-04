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
}
