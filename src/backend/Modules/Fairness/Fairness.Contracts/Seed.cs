namespace Apogeu.Fairness.Contracts;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): every member is implemented in the green phase.

/// <summary>
/// A 32-byte link of the seed chain (a server seed, a commitment or the terminal hash). Any other length throws
/// <see cref="InvalidSeedException"/>. Unrevealed seeds are secret (ADR-0014): <see cref="ToString"/> never shows the bytes.
/// </summary>
public sealed class Seed : IEquatable<Seed>
{
    /// <summary>Size of a seed in bytes (a SHA-256 digest).</summary>
    public const int Length = 32;

    public Seed(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }

    /// <summary>Parses the lowercase or uppercase hex of the 32 bytes.</summary>
    public static Seed FromHex(string hex) =>
        throw new NotImplementedException();

    /// <summary>A copy of the 32 raw bytes.</summary>
    public byte[] ToArray() =>
        throw new NotImplementedException();

    /// <summary>The lowercase hex of the 32 bytes.</summary>
    public string ToHex() =>
        throw new NotImplementedException();

    public bool Equals(Seed? other) =>
        throw new NotImplementedException();

    public override bool Equals(object? obj) => Equals(obj as Seed);

    public override int GetHashCode() =>
        throw new NotImplementedException();

    /// <summary>Never the seed bytes: seeds must not leak to logs through string formatting.</summary>
    public override string ToString() =>
        throw new NotImplementedException();

    public static bool operator ==(Seed? left, Seed? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Seed? left, Seed? right) => !(left == right);
}
#pragma warning restore MA0025
