using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>
/// Test oracle: algorithm v1 of SPEC-0007 §4 written literally with BigInteger, independent of Fairness.Domain, so the tests
/// never derive their expected values from the code under test. The vectors file has its own oracle in Python
/// (tests/vectors/generate_fairness_v1.py).
/// </summary>
internal static class ReferenceAlgorithm
{
    private static readonly BigInteger SampleSpace = BigInteger.One << 52;

    /// <summary>h_{k+1} = SHA256(h_k) over the raw 32 bytes.</summary>
    public static byte[] NextLink(byte[] link) => SHA256.HashData(link);

    /// <summary>The chain h_0..h_length from the origin h_0 (length + 1 links).</summary>
    public static byte[][] Chain(byte[] origin, int length)
    {
        var chain = new byte[length + 1][];
        chain[0] = origin;
        for (var k = 1; k <= length; k++)
        {
            chain[k] = NextLink(chain[k - 1]);
        }

        return chain;
    }

    /// <summary>X: the first 52 bits of HMAC-SHA256(key = seed, message = salt bytes), big-endian.</summary>
    public static long Sample(byte[] seed, byte[] salt)
    {
        var digest = HMACSHA256.HashData(seed, salt);
        return (long)(BinaryPrimitives.ReadUInt64BigEndian(digest) >> 12);
    }

    /// <summary>min(cap, max(1.00, floor(100 · RTP · E / (E − X)) / 100)) with RTP as an exact decimal fraction.</summary>
    public static decimal CrashPoint(long sample, decimal rtp, decimal cap)
    {
        var denominator = BigInteger.Pow(10, rtp.Scale);
        var numerator = new BigInteger(rtp * (decimal)denominator);
        var raw = BigInteger.Divide(100 * numerator * SampleSpace, denominator * (SampleSpace - sample));
        var capHundredths = new BigInteger(cap * 100m);
        var hundredths = BigInteger.Min(capHundredths, BigInteger.Max(100, raw));
        return (decimal)hundredths / 100m;
    }

    public static decimal CrashPoint(byte[] seed, byte[] salt, decimal rtp, decimal cap) =>
        CrashPoint(Sample(seed, salt), rtp, cap);
}
