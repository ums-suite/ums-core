namespace UMS.Modules.Identity.Api.Contracts;

/// <summary>
/// HTTP-facing login/refresh request shapes - deliberately narrower than the Application layer's
/// own <c>LoginRequest</c>/<c>RefreshRequest</c>, which also carry <c>UserAgent</c>/<c>IpAddress</c>
/// populated server-side from the request itself, never from the client-supplied JSON body.
/// </summary>
public sealed record LoginRequestBody(string Identifier, string Password);

public sealed record RefreshRequestBody(string RefreshToken);

public sealed record ChangeUserStatusRequestBody(string Status);
