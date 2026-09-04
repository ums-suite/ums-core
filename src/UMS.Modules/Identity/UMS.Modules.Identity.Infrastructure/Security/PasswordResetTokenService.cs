using System.Security.Cryptography;
using UMS.Modules.Identity.Application.Abstractions;

namespace UMS.Modules.Identity.Infrastructure.Security;

/// <summary>IDN-12: mirrors <see cref="JwtTokenService"/>'s own refresh-token plaintext/SHA-256-hash split exactly (design-decisions.md, "Token/Session Storage &amp; Rotation Mechanism").</summary>
public sealed class PasswordResetTokenService : IPasswordResetTokenService
{
    private const int TokenLengthBytes = 32;

    public (string PlainText, string Hash) IssueToken()
    {
        var plainText = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenLengthBytes));
        return (plainText, Hash(plainText));
    }

    public string Hash(string plainTextToken) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainTextToken)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
