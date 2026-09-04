namespace UMS.Modules.Identity.Domain.Users;

/// <summary>
/// A <see cref="User"/>'s single outstanding password-reset token (requirement-spec.md identity
/// §2 "single-use, short-TTL"; edge-cases.md "Concurrent password-reset requests" - issuing a new
/// token always fully replaces this slot, so at most one token is ever valid at a time). Carries
/// only the token's hash, never the plaintext (mirrors <see cref="Sessions.Session"/>'s own
/// refresh-token-hash storage pattern).
/// </summary>
public sealed record PasswordResetChallenge(string TokenHash, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? ConsumedAt)
{
    public bool IsUsable(string presentedTokenHash, DateTimeOffset now) =>
        ConsumedAt is null
        && now <= ExpiresAt
        && string.Equals(TokenHash, presentedTokenHash, StringComparison.Ordinal);

    public PasswordResetChallenge Consumed(DateTimeOffset now) => this with { ConsumedAt = now };
}
