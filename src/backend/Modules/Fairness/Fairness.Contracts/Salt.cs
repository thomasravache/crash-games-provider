namespace Apogeu.Fairness.Contracts;

/// <summary>The public random salt of a chain: the HMAC message of algorithm v1 is its raw bytes (not its hex text).</summary>
public sealed class Salt : IEquatable<Salt>
{
    private readonly byte[] _bytes;

    public Salt(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    /// <summary>Parses the hex of the salt bytes.</summary>
    public static Salt FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        return new Salt(Convert.FromHexString(hex));
    }

    /// <summary>The raw bytes, read-only and without copying.</summary>
    public ReadOnlySpan<byte> AsSpan() => _bytes;

    /// <summary>A copy of the raw bytes.</summary>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    /// <summary>The lowercase hex of the bytes.</summary>
    public string ToHex() => Convert.ToHexStringLower(_bytes);

    public bool Equals(Salt? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => Equals(obj as Salt);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    /// <summary>The salt is public: its hex.</summary>
    public override string ToString() => ToHex();

    public static bool operator ==(Salt? left, Salt? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Salt? left, Salt? right) => !(left == right);
}
