namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>requirement-spec.md identity §9.3: TOTP (RFC 6238), the only MFA method for v1.</summary>
public interface ITotpGenerator
{
    /// <summary>A fresh, cryptographically random 160-bit secret (RFC 6238's own recommended minimum for HMAC-SHA1).</summary>
    public byte[] GenerateSecret();

    /// <summary>An <c>otpauth://</c> URI an authenticator app renders as the enrollment QR code.</summary>
    public string BuildOtpAuthUri(byte[] secret, string accountLabel, string issuer);

    /// <summary>The Base32 text form shown once for manual entry, alongside the QR code (RFC 6238's own encoding for a human-typeable secret).</summary>
    public string EncodeSecretForDisplay(byte[] secret);

    /// <summary>Verifies a 6-digit code against <paramref name="secret"/>, tolerating ordinary clock drift (typically ±1 time step).</summary>
    public bool VerifyCode(byte[] secret, string code);
}
