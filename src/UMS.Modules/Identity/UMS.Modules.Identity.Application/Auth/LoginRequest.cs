namespace UMS.Modules.Identity.Application.Auth;

public sealed record LoginRequest(string Identifier, string Password, string? UserAgent, string? IpAddress);

public sealed record RefreshRequest(string RefreshToken, string? UserAgent, string? IpAddress);

public sealed record TokenPairResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId);

/// <summary>
/// IDN-11/§8 "MFA-required role assigned to a User without MFA enrolled": login either completes
/// immediately (<see cref="LoginSucceeded"/>, the pre-MFA wire shape, unchanged) or, when an active
/// Role requires MFA, is suspended pending <c>POST /auth/mfa/verify</c> (<see cref="LoginRequiresMfa"/>) -
/// never a silent, confusing permission denial.
/// </summary>
public abstract record LoginOutcome;

public sealed record LoginSucceeded(TokenPairResult Tokens) : LoginOutcome;

/// <summary><paramref name="MfaEnrolled"/> tells the caller's UI whether to render "enter your code" or "scan this QR code first."</summary>
public sealed record LoginRequiresMfa(string MfaChallengeToken, DateTimeOffset MfaChallengeTokenExpiresAt, bool MfaEnrolled) : LoginOutcome;
