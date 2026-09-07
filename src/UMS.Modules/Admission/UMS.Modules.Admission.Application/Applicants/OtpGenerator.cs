using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UMS.Modules.Admission.Application.Applicants;

/// <summary>ADM-3: a random 6-digit code and its stored hash - mirrors Identity's own password-reset-token mechanism (a random value, only its hash ever persisted).</summary>
internal static class OtpGenerator
{
    public static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static string Hash(Guid applicantId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{applicantId:N}:{code}")));
}
