namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Adaptive password hashing port (requirement-spec.md identity §2: "never reversible encryption,
/// never a fast general-purpose hash"; design-decisions.md, "Password Hashing Algorithm Choice" -
/// Argon2id). The concrete algorithm lives entirely in Infrastructure so the Domain/Application
/// layers never depend on a specific crypto library.
/// </summary>
public interface IPasswordHasher
{
    public string AlgorithmName { get; }

    public string HashPassword(string plaintextPassword);

    public bool VerifyPassword(string plaintextPassword, string hash);
}
