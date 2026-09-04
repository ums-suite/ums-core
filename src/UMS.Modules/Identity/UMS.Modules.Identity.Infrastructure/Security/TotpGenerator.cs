using OtpNet;
using UMS.Modules.Identity.Application.Abstractions;

namespace UMS.Modules.Identity.Infrastructure.Security;

/// <summary>requirement-spec.md identity §9.3: TOTP (RFC 6238) via Otp.NET - see this module's own <c>Identity:Mfa</c> options for the issuer label and design-decisions.md's "MFA Secret Storage" for why the secret this returns is never persisted as-is (see <see cref="IMfaSecretEncryptor"/>).</summary>
public sealed class TotpGenerator : ITotpGenerator
{
    private const int SecretLengthBytes = 20;

    public byte[] GenerateSecret() => KeyGeneration.GenerateRandomKey(SecretLengthBytes);

    public string BuildOtpAuthUri(byte[] secret, string accountLabel, string issuer)
    {
        var base32Secret = EncodeSecretForDisplay(secret);
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedLabel = Uri.EscapeDataString(accountLabel);
        return $"otpauth://totp/{encodedIssuer}:{encodedLabel}?secret={base32Secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public string EncodeSecretForDisplay(byte[] secret) => Base32Encoding.ToString(secret);

    public bool VerifyCode(byte[] secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var totp = new Totp(secret);

        // One time-step of tolerance on either side of "now" - accommodates ordinary clock drift
        // between server and authenticator app without meaningfully widening the guessable window
        // (each step is 30s, so this is a ±30s tolerance).
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
