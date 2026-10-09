using System.Security.Cryptography;

namespace Apogeu.Fairness.Contracts;

/// <summary>
/// A 32-byte link of the seed chain (a server seed, a commitment or the terminal hash). Any other length throws
/// <see cref="InvalidSeedException"/>. Unrevealed seeds are secret (ADR-0014): <see cref="ToString"/> never shows the bytes.
/// </summary>
public sealed class Seed : IEquatable<Seed>
{
    /// <summary>Size of a seed in bytes (a SHA-256 digest).</summary>
    public const int Length = 32;

    private readonly byte[] _bytes;

    public Seed(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length)
        {
            throw new InvalidSeedException($"A seed has {Length} bytes; {bytes.Length} bytes were given.");
        }

        _bytes = bytes.ToArray();
    }

    /// <summary>Parses the lowercase or uppercase hex of the 32 bytes.</summary>
    public static Seed FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(hex);
        }
        catch (FormatException exception)
        {
            // The message never echoes the input: it may be a secret seed.
            throw new InvalidSeedException($"A seed is the hex of {Length} bytes.", exception);
        }

        try
        {
            return new Seed(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>The raw bytes, read-only and without copying (the caller must not keep or log them).</summary>
    public ReadOnlySpan<byte> AsSpan() => _bytes;

    /// <summary>A copy of the 32 raw bytes.</summary>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    /// <summary>The lowercase hex of the 32 bytes.</summary>
    public string ToHex() => Convert.ToHexStringLower(_bytes);

    /// <summary>Constant-time comparison of the bytes.</summary>
    public bool Equals(Seed? other) => other is not null && CryptographicOperations.FixedTimeEquals(_bytes, other._bytes);

    public override bool Equals(object? obj) => Equals(obj as Seed);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    /// <summary>Never the seed bytes: seeds must not leak to logs through string formatting.</summary>
    public override string ToString() => "Seed(redacted)";

    public static bool operator ==(Seed? left, Seed? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Seed? left, Seed? right) => !(left == right);
}
