namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// design-decisions.md, "MFA Secret Storage": envelope encryption for every TOTP secret, pending
/// or enrolled - a per-record data key wraps the secret, itself wrapped by a rotatable platform
/// master key. The Domain/Application layers only ever see the opaque ciphertext this produces,
/// never a plaintext secret or the master key itself.
/// </summary>
public interface IMfaSecretEncryptor
{
    public string Encrypt(byte[] plaintextSecret);

    public byte[] Decrypt(string cipherText);
}
