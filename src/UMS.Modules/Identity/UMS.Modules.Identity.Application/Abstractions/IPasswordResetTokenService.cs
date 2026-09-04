namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// IDN-12: mints/hashes the single-use password-reset token (requirement-spec.md identity §2
/// "single-use, short-TTL"). Mirrors <see cref="ITokenService"/>'s own refresh-token plaintext/hash
/// split exactly - only the hash is ever persisted (<see cref="Domain.Users.PasswordResetChallenge"/>).
/// </summary>
public interface IPasswordResetTokenService
{
    public (string PlainText, string Hash) IssueToken();

    public string Hash(string plainTextToken);
}
