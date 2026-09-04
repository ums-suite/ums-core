using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application;
using UMS.Modules.Identity.Application.Abstractions;

namespace UMS.Modules.Identity.Infrastructure.Security;

/// <summary>
/// design-decisions.md, "MFA Secret Storage": envelope encryption via AES-256-GCM at both layers -
/// a fresh, random 32-byte data key encrypts the TOTP secret itself, and that data key is in turn
/// encrypted ("wrapped") by <see cref="IdentityMfaOptions.MasterKeyBase64"/>, the platform master
/// key. Master-key rotation re-wraps only the (tiny) data key, never the secret itself, without
/// forcing every enrolled user to re-enroll - the property this design decision names as its whole
/// reason for choosing envelope encryption over a single static application secret.
///
/// <para>
/// Ciphertext is serialized as four base64 segments joined by <c>.</c> - <c>wrapNonce.wrappedDataKey.secretNonce.encryptedSecret</c> -
/// self-describing the same way <see cref="Argon2idPasswordHasher"/>'s stored hash string is,
/// rather than requiring a separate schema/column per component.
/// </para>
/// </summary>
public sealed class EnvelopeMfaSecretEncryptor(IOptions<IdentityMfaOptions> options) : IMfaSecretEncryptor
{
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int DataKeyLength = 32;

    public string Encrypt(byte[] plaintextSecret)
    {
        var masterKey = ResolveMasterKey();
        var dataKey = RandomNumberGenerator.GetBytes(DataKeyLength);

        var (secretNonce, encryptedSecret) = AesGcmEncrypt(dataKey, plaintextSecret);
        var (wrapNonce, wrappedDataKey) = AesGcmEncrypt(masterKey, dataKey);

        return string.Join(
            '.',
            Convert.ToBase64String(wrapNonce),
            Convert.ToBase64String(wrappedDataKey),
            Convert.ToBase64String(secretNonce),
            Convert.ToBase64String(encryptedSecret));
    }

    public byte[] Decrypt(string cipherText)
    {
        var parts = cipherText.Split('.');
        if (parts.Length != 4)
        {
            throw new FormatException("Malformed MFA secret ciphertext - expected 4 base64 segments.");
        }

        var masterKey = ResolveMasterKey();
        var wrapNonce = Convert.FromBase64String(parts[0]);
        var wrappedDataKey = Convert.FromBase64String(parts[1]);
        var secretNonce = Convert.FromBase64String(parts[2]);
        var encryptedSecret = Convert.FromBase64String(parts[3]);

        var dataKey = AesGcmDecrypt(masterKey, wrapNonce, wrappedDataKey);
        return AesGcmDecrypt(dataKey, secretNonce, encryptedSecret);
    }

    private static (byte[] Nonce, byte[] CipherTextAndTag) AesGcmEncrypt(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var cipherText = new byte[plaintext.Length];
        var tag = new byte[TagLength];

        using var aesGcm = new AesGcm(key, TagLength);
        aesGcm.Encrypt(nonce, plaintext, cipherText, tag);

        var cipherTextAndTag = new byte[cipherText.Length + tag.Length];
        cipherText.CopyTo(cipherTextAndTag, 0);
        tag.CopyTo(cipherTextAndTag, cipherText.Length);
        return (nonce, cipherTextAndTag);
    }

    private static byte[] AesGcmDecrypt(byte[] key, byte[] nonce, byte[] cipherTextAndTag)
    {
        var cipherTextLength = cipherTextAndTag.Length - TagLength;
        var cipherText = cipherTextAndTag.AsSpan(0, cipherTextLength);
        var tag = cipherTextAndTag.AsSpan(cipherTextLength, TagLength);
        var plaintext = new byte[cipherTextLength];

        using var aesGcm = new AesGcm(key, TagLength);
        aesGcm.Decrypt(nonce, cipherText, tag, plaintext);
        return plaintext;
    }

    private byte[] ResolveMasterKey()
    {
        var configured = options.Value.MasterKeyBase64;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Missing required 'Identity:Mfa:MasterKeyBase64' configuration value.");
        }

        var key = Convert.FromBase64String(configured);
        if (key.Length != DataKeyLength)
        {
            throw new InvalidOperationException($"'Identity:Mfa:MasterKeyBase64' must decode to exactly {DataKeyLength} bytes (AES-256) - got {key.Length}.");
        }

        return key;
    }
}
