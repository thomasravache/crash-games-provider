using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

/// <summary>
/// The crash point of algorithm v1 (SPEC-0007 §4): H = HMAC-SHA256(key = seed, message = salt bytes), X = the first 52 bits
/// of H, E = 2^52, crash = min(cap, max(1.00, floor(100 · RTP · E / (E − X)) / 100)) in exact integer arithmetic.
/// </summary>
public static class CrashPointCalculator
{
    /// <summary>Bits of the HMAC used as the sample X.</summary>
    public const int SampleBits = 52;

    /// <summary>E = 2^52, the size of the sample space.</summary>
    public const long SampleSpace = 1L << SampleBits;

    private const int HundredthsPerUnit = 100;
    private const decimal Hundredth = 0.01m;

    /// <summary>The crash point of the seed and salt for the RTP and cap.</summary>
    public static CrashPoint Compute(Seed seed, Salt salt, Rtp rtp, Multiplier cap)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(salt);

        return FromSample(Sample(seed, salt), rtp, cap);
    }

    /// <summary>X: the first 52 bits of HMAC-SHA256(key = seed, message = salt bytes), read big-endian.</summary>
    public static long Sample(Seed seed, Salt salt)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(salt);

        Span<byte> digest = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(seed.AsSpan(), salt.AsSpan(), digest);
        return (long)(BinaryPrimitives.ReadUInt64BigEndian(digest) >> (64 - SampleBits));
    }

    /// <summary>The crash point of a sample X in [0, 2^52) for the RTP and cap (step 4 of the algorithm).</summary>
    public static CrashPoint FromSample(long sample, Rtp rtp, Multiplier cap)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sample);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sample, SampleSpace);
        var capHundredths = CapInHundredths(cap);
        var (rtpNumerator, rtpDenominator) = RtpFraction(rtp);

        // floor(100 · RTP · E / (E − X)) with RTP = numerator / denominator, all integers.
        var hundredths = BigInteger.Divide(
            HundredthsPerUnit * rtpNumerator * SampleSpace,
            rtpDenominator * (SampleSpace - sample));
        hundredths = BigInteger.Min(capHundredths, BigInteger.Max(HundredthsPerUnit, hundredths));

        return new CrashPoint((long)hundredths * Hundredth);
    }

    /// <summary>The RTP as an exact fraction of integers; rejects a value outside [0.90, 0.99] (such as a default Rtp).</summary>
    private static (BigInteger Numerator, BigInteger Denominator) RtpFraction(Rtp rtp)
    {
        if (rtp.Value is < Rtp.Minimum or > Rtp.Maximum)
        {
            throw new InvalidRtpException("The RTP must be between 0.90 and 0.99.");
        }

        return Fraction(rtp.Value);
    }

    /// <summary>floor(100 · cap): the cap in hundredths; a cap below 1.00 throws <see cref="InvalidCapException"/>.</summary>
    private static BigInteger CapInHundredths(Multiplier cap)
    {
        if (cap.Value < 1.00m)
        {
            throw new InvalidCapException("The cap must be at least 1.00.");
        }

        var (numerator, denominator) = Fraction(cap.Value);
        return BigInteger.Divide(HundredthsPerUnit * numerator, denominator);
    }

    /// <summary>A non-negative decimal as mantissa / 10^scale.</summary>
    private static (BigInteger Numerator, BigInteger Denominator) Fraction(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var mantissa = new BigInteger((uint)bits[0])
            | (new BigInteger((uint)bits[1]) << 32)
            | (new BigInteger((uint)bits[2]) << 64);
        return (mantissa, BigInteger.Pow(10, value.Scale));
    }
}
