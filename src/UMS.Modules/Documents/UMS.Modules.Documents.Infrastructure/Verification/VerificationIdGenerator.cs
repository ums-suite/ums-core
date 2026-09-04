using System.Security.Cryptography;
using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.Infrastructure.Verification;

/// <summary>
/// DOC-8: 20 bytes (160 bits) of cryptographically secure randomness, Base32-Crockford-encoded
/// (no padding, no ambiguous characters) - "unique and unguessable" (requirement-spec.md documents
/// §2/§4). 160 bits of entropy makes brute-force guessing of a real id computationally infeasible,
/// and the encoding is URL-safe with no percent-encoding needed in the public verify endpoint's
/// path segment.
/// </summary>
internal sealed class VerificationIdGenerator : IVerificationIdGenerator
{
    private const string Base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string NewId()
    {
        Span<byte> bytes = stackalloc byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Encode(bytes);
    }

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        // 160 bits encoded 5 bits at a time = 32 Base32 characters exactly, no padding needed.
        Span<char> chars = stackalloc char[32];
        var bitBuffer = 0UL;
        var bitsInBuffer = 0;
        var byteIndex = 0;
        var charIndex = 0;

        while (charIndex < chars.Length)
        {
            if (bitsInBuffer < 5 && byteIndex < bytes.Length)
            {
                bitBuffer = (bitBuffer << 8) | bytes[byteIndex++];
                bitsInBuffer += 8;
            }

            var shift = bitsInBuffer - 5;
            var index = shift >= 0
                ? (int)((bitBuffer >> shift) & 0x1F)
                : (int)((bitBuffer << -shift) & 0x1F);

            chars[charIndex++] = Base32Alphabet[index];
            bitsInBuffer -= 5;
            if (bitsInBuffer < 0)
            {
                bitsInBuffer = 0;
            }
        }

        return new string(chars);
    }
}
