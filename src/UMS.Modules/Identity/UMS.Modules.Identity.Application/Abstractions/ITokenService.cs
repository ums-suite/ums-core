using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;

namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Issues/validates the OAuth2/OIDC-shaped token pair (requirement-spec.md identity §2, ADR-0005).
/// The JWT carries identity claims only (`sub`, `sid`, `roles`) - never a resolved permission
/// snapshot (design-decisions.md, "Permission-Check Caching vs. Live Lookup").
/// </summary>
public interface ITokenService
{
    public IssuedAccessToken IssueAccessToken(UserId userId, SessionId sessionId, IReadOnlyCollection<string> roleNames, DateTimeOffset now);

    /// <summary>
    /// IDN-11: a short-lived, narrowly-scoped token issued mid-login when an active Role requires
    /// MFA - carries only <c>sub</c> and a distinguishing "mfa challenge" claim, never <c>sid</c>/
    /// <c>roles</c> (no real Session exists yet). Valid only against <c>POST /auth/mfa/enroll</c>/
    /// <c>verify</c>, never any other protected endpoint (design-decisions.md's shared-middleware
    /// convention still authenticates it; those two endpoints are the only ones that treat its
    /// distinguishing claim as sufficient in place of a live Session).
    /// </summary>
    public IssuedAccessToken IssueMfaChallengeToken(UserId userId, DateTimeOffset now);

    /// <summary>Plaintext shape embeds the SessionId as a lookup prefix so refresh never has to scan-by-hash (design-decisions.md, "Token/Session Storage &amp; Rotation Mechanism").</summary>
    public IssuedRefreshToken IssueRefreshToken(SessionId sessionId, DateTimeOffset now);

    public string HashRefreshToken(string plaintextRefreshToken);

    public bool TryExtractSessionId(string plaintextRefreshToken, out SessionId sessionId);
}

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record IssuedRefreshToken(string PlaintextValue, string Hash, DateTimeOffset ExpiresAt);
