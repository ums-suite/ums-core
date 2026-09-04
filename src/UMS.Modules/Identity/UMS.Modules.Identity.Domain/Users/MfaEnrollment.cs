namespace UMS.Modules.Identity.Domain.Users;

/// <summary>
/// A <see cref="User"/>'s TOTP MFA state (requirement-spec.md identity §2 MFA, §9.3 TOTP-only for
/// v1). At most one pending (unverified) secret exists at a time - a fresh
/// <see cref="User.BeginMfaEnrollment"/> call always replaces whatever was pending before
/// (edge-cases.md, "MFA enrollment interrupted mid-flow"), and a pending secret expires on its own
/// 10 minutes after being issued regardless of whether it is ever replaced.
///
/// <para>
/// <see cref="PendingSecretCipherText"/>/<see cref="EnrolledSecretCipherText"/> are opaque,
/// envelope-encrypted blobs (design-decisions.md, "MFA Secret Storage") - this type never sees a
/// plaintext TOTP secret, only <see cref="Abstractions.IMfaSecretEncryptor"/>'s ciphertext output.
/// </para>
/// </summary>
public sealed record MfaEnrollment(
    string? PendingSecretCipherText,
    DateTimeOffset? PendingCreatedAt,
    DateTimeOffset? PendingExpiresAt,
    string? EnrolledSecretCipherText,
    DateTimeOffset? EnrolledAt)
{
    public static MfaEnrollment Empty { get; } = new(null, null, null, null, null);

    /// <summary>§4 invariant "MFA cannot be silently downgraded" - a Role requiring MFA only resolves its Permissions once this is true.</summary>
    public bool IsEnrolled => EnrolledSecretCipherText is not null;

    public bool HasUnexpiredPendingSecret(DateTimeOffset now) => PendingSecretCipherText is not null && now <= PendingExpiresAt;

    public MfaEnrollment WithPendingSecret(string cipherText, DateTimeOffset now, TimeSpan ttl) =>
        this with { PendingSecretCipherText = cipherText, PendingCreatedAt = now, PendingExpiresAt = now + ttl };

    /// <summary>Promotes the pending secret to the enrolled (active) one, replacing any prior enrolled secret - the caller has already verified a code against <see cref="PendingSecretCipherText"/>.</summary>
    public MfaEnrollment PromotePendingToEnrolled(DateTimeOffset now) =>
        this with { EnrolledSecretCipherText = PendingSecretCipherText, EnrolledAt = now, PendingSecretCipherText = null, PendingCreatedAt = null, PendingExpiresAt = null };
}
