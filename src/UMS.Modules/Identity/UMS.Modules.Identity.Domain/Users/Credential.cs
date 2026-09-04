namespace UMS.Modules.Identity.Domain.Users;

/// <summary>
/// The password hash backing a <see cref="User"/>'s ability to authenticate
/// (requirement-spec.md identity §3: "Password hash and/or external-provider link, owned by
/// exactly one User"). Never holds a plaintext password or a reversibly-encrypted one
/// (design-decisions.md, "Password Hashing Algorithm Choice") - hashing itself is an
/// infrastructure concern behind the Application layer's <c>IPasswordHasher</c> port; this value
/// object only carries the already-hashed result plus which algorithm produced it, so a future
/// algorithm migration can be recognized per-record rather than assumed uniform.
/// </summary>
public sealed record Credential
{
    private Credential(string passwordHash, string algorithm, DateTimeOffset changedAt)
    {
        PasswordHash = passwordHash;
        Algorithm = algorithm;
        ChangedAt = changedAt;
    }

    public string PasswordHash { get; }

    public string Algorithm { get; }

    public DateTimeOffset ChangedAt { get; }

    public static Credential FromHash(string passwordHash, string algorithm, DateTimeOffset changedAt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A Credential must carry a non-empty password hash.", nameof(passwordHash));
        }

        if (string.IsNullOrWhiteSpace(algorithm))
        {
            throw new ArgumentException("A Credential must record which algorithm produced its hash.", nameof(algorithm));
        }

        return new Credential(passwordHash, algorithm, changedAt);
    }
}
