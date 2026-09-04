namespace UMS.Modules.Identity.Application.Auth;

public sealed record LoginRequest(string Identifier, string Password, string? UserAgent, string? IpAddress);

public sealed record RefreshRequest(string RefreshToken, string? UserAgent, string? IpAddress);

public sealed record TokenPairResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId);
