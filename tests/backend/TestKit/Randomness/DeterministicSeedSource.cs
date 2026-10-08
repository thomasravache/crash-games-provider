using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Apogeu.TestKit.Randomness;

/// <summary>
/// Reproducible seeds for tests (ADR-0009): the same <paramref name="label"/> always yields the same sequence, on any
/// machine and runtime, because each seed is SHA-256(label, counter) instead of a pseudo-random generator. Never used
/// outside tests: production seeds come from RandomNumberGenerator (ADR-0006).
/// </summary>
public sealed class DeterministicSeedSource(string label)
{
    private readonly byte[] _label = Encoding.UTF8.GetBytes(label);
    private long _counter;

    /// <summary>The next seed of the sequence (32 bytes, the size of a SHA-256 output).</summary>
    public byte[] NextSeed()
    {
        var input = new byte[_label.Length + sizeof(long)];
        _label.CopyTo(input, 0);
        BinaryPrimitives.WriteInt64BigEndian(input.AsSpan(_label.Length), Interlocked.Increment(ref _counter));
        return SHA256.HashData(input);
    }
}
