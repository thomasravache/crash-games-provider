namespace Apogeu.Fairness.Contracts;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): every member is implemented in the green phase.

/// <summary>The public random salt of a chain: the HMAC message of algorithm v1 is its raw bytes (not its hex text).</summary>
public sealed class Salt : IEquatable<Salt>
{
    public Salt(ReadOnlySpan<byte> bytes)
    {
        throw new NotImplementedException();
    }

    /// <summary>Parses the hex of the salt bytes.</summary>
    public static Salt FromHex(string hex) =>
        throw new NotImplementedException();

    /// <summary>A copy of the raw bytes.</summary>
    public byte[] ToArray() =>
        throw new NotImplementedException();

    /// <summary>The lowercase hex of the bytes.</summary>
    public string ToHex() =>
        throw new NotImplementedException();

    public bool Equals(Salt? other) =>
        throw new NotImplementedException();

    public override bool Equals(object? obj) => Equals(obj as Salt);

    public override int GetHashCode() =>
        throw new NotImplementedException();

    public override string ToString() =>
        throw new NotImplementedException();

    public static bool operator ==(Salt? left, Salt? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Salt? left, Salt? right) => !(left == right);
}
#pragma warning restore MA0025
