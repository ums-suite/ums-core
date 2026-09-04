using System.Security.Cryptography;

namespace UMS.Modules.Audit.Domain.Common;

/// <summary>
/// A minimal, process-local, monotonic ULID generator (design-decisions.md, "Ordering/Sequencing
/// Mechanism": "the ULID already used for `id`... lexicographically sortable, embedding a
/// millisecond timestamp plus randomness, giving a stable total order even for same-millisecond
/// entries"). Deliberately hand-rolled rather than a NuGet dependency - the full ULID spec's only
/// properties this module actually needs are (1) 128 bits, (2) a 48-bit big-endian millisecond
/// timestamp prefix so lexicographic string order matches chronological order, and (3) enough
/// trailing entropy that two calls landing in the same millisecond still sort in true call order
/// rather than colliding or inverting.
///
/// <para>
/// Monotonicity is guaranteed only within one process (a lock-guarded static counter that
/// increments the random tail when the clock millisecond hasn't advanced since the last call) -
/// sufficient here because every <c>RecordEntry</c> call happens in-process (ADR-0001's
/// modular-monolith model; edge-cases.md's ordering decision already accepts that true
/// cross-process causal ordering is a bounded, out-of-scope-for-v1 limitation).
/// </para>
/// </summary>
public static class Ulid
{
    // Crockford's Base32 alphabet (excludes I, L, O, U to avoid transcription ambiguity) - the
    // same alphabet the ULID spec itself specifies.
    private const string Base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly object SyncRoot = new();
    private static readonly byte[] LastRandom = new byte[10];
    private static long _lastTimestampMs = -1;

    public static string NewUlid()
    {
        Span<byte> bytes = stackalloc byte[16];

        lock (SyncRoot)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (now > _lastTimestampMs)
            {
                _lastTimestampMs = now;
                RandomNumberGenerator.Fill(LastRandom);
            }
            else
            {
                // Same millisecond as the previous call in this process: increment the random
                // tail as a big-endian counter so this call's ULID still sorts strictly after the
                // previous one (monotonic), instead of drawing fresh, unordered randomness.
                IncrementRandomTail();
            }

            WriteTimestamp(bytes, _lastTimestampMs);
            LastRandom.CopyTo(bytes[6..]);
        }

        return Encode(bytes);
    }

    private static void IncrementRandomTail()
    {
        for (var i = LastRandom.Length - 1; i >= 0; i--)
        {
            if (++LastRandom[i] != 0)
            {
                return;
            }

            // Overflowed this byte (wrapped 0xFF -> 0x00) - carry into the next-more-significant
            // byte. In the astronomically unlikely case all 10 bytes overflow (2^80 calls inside
            // one millisecond), the tail simply wraps to all-zero - an accepted, undetectable
            // edge case no v1 requirement asks this generator to guard against.
        }
    }

    private static void WriteTimestamp(Span<byte> destination, long timestampMs)
    {
        for (var i = 5; i >= 0; i--)
        {
            destination[i] = (byte)(timestampMs & 0xFF);
            timestampMs >>= 8;
        }
    }

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        // 128 bits encoded 5 bits at a time = 26 Base32 characters (the last character carries
        // only 2 significant bits, matching the canonical 26-character ULID text length).
        Span<char> chars = stackalloc char[26];
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
