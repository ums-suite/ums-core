using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UMS.Modules.Identity.Application;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Infrastructure.Security;

/// <summary>
/// IDN-5/IDN-6: issues the OAuth2/OIDC-shaped token pair (requirement-spec.md identity §2,
/// ADR-0005). Reads the same <c>Jwt:*</c> configuration section
/// <see cref="UMS.Shared.Authorization.DependencyInjection.AddUmsAuthentication"/> validates
/// against, so Identity issuing and every module's shared middleware validating are always
/// talking about the same signing key/issuer/audience.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly IdentityTokenOptions _tokenOptions;

    public JwtTokenService(IConfiguration configuration, IOptions<IdentityTokenOptions> tokenOptions)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var signingKey = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException("Missing required 'Jwt:SigningKey' configuration value.");
        _issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException("Missing required 'Jwt:Issuer' configuration value.");
        _audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException("Missing required 'Jwt:Audience' configuration value.");
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        _tokenOptions = tokenOptions.Value;
    }

    public IssuedAccessToken IssueAccessToken(UserId userId, SessionId sessionId, IReadOnlyCollection<string> roleNames, DateTimeOffset now)
    {
        var expiresAt = now + _tokenOptions.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(UmsClaimTypes.Subject, userId.Value.ToString()),
            new(UmsClaimTypes.SessionId, sessionId.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roleNames.Select(role => new Claim(UmsClaimTypes.Roles, role)));

        var token = new JwtSecurityToken(
            _issuer,
            _audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// Plaintext shape is <c>{sessionId:N}.{random-secret}</c> - the prefix lets
    /// <see cref="TryExtractSessionId"/> find the right <see cref="Session"/> row directly instead
    /// of scanning every session's hash (design-decisions.md, "Token/Session Storage &amp;
    /// Rotation Mechanism").
    /// </summary>
    public IssuedRefreshToken IssueRefreshToken(SessionId sessionId, DateTimeOffset now)
    {
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var plaintext = $"{sessionId.Value:N}.{secret}";
        var expiresAt = now + _tokenOptions.RefreshTokenLifetime;

        return new IssuedRefreshToken(plaintext, HashRefreshToken(plaintext), expiresAt);
    }

    public string HashRefreshToken(string plaintextRefreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextRefreshToken));
        return Convert.ToHexString(bytes);
    }

    public bool TryExtractSessionId(string plaintextRefreshToken, out SessionId sessionId)
    {
        sessionId = default;
        var separatorIndex = plaintextRefreshToken.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return false;
        }

        if (!Guid.TryParseExact(plaintextRefreshToken[..separatorIndex], "N", out var guid))
        {
            return false;
        }

        sessionId = new SessionId(guid);
        return true;
    }
}
