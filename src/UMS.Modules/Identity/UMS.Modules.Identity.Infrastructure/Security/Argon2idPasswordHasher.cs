using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application.Abstractions;

namespace UMS.Modules.Identity.Infrastructure.Security;

/// <summary>
/// IDN-2/design-decisions.md ("Password Hashing Algorithm Choice"): Argon2id, memory-hard,
/// cost-tunable. The stored hash string self-describes its own cost parameters
/// (<c>memoryKb.iterations.parallelism.saltBase64.hashBase64</c>) so a future re-tune of
/// <see cref="Argon2idOptions"/> never breaks verification of passwords hashed under the old
/// parameters.
/// </summary>
public sealed class Argon2idPasswordHasher(IOptions<Argon2idOptions> options) : IPasswordHasher
{
    private const int SaltLength = 16;
    private const int HashLength = 32;

    public string AlgorithmName => "argon2id";

    public string HashPassword(string plaintextPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var cost = options.Value;
        var hash = ComputeHash(plaintextPassword, salt, cost.MemorySizeKb, cost.Iterations, cost.Parallelism, HashLength);

        return string.Join(
            '.',
            cost.MemorySizeKb,
            cost.Iterations,
            cost.Parallelism,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool VerifyPassword(string plaintextPassword, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 5
            || !int.TryParse(parts[0], out var memoryKb)
            || !int.TryParse(parts[1], out var iterations)
            || !int.TryParse(parts[2], out var parallelism))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = ComputeHash(plaintextPassword, salt, memoryKb, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int hashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb,
        };

        return argon2.GetBytes(hashLength);
    }
}
