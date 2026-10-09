using System.Buffers.Binary;
using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Xunit;

namespace Apogeu.Fairness.Tests.Mutation;

/// <summary>
/// UT-02 of SPEC-0016: <see cref="CrashPointCalculator"/> on the edges of algorithm v1 (SPEC-0007 §4) — the 1.00 floor,
/// the cap, the exact floor of the hundredths, caps whose mantissa needs more than 32 or 64 bits — and its guard clauses
/// (null arguments, sample outside [0, 2^52), InvalidRtp, InvalidCap).
/// </summary>
public sealed class CrashPointCalculatorMutationTests
{
    private const string Tag = "SPEC-0016:UT-02";
    private const long E = CrashPointCalculator.SampleSpace;

    private static readonly Rtp Rtp99 = new(0.99m);
    private static readonly Multiplier Cap = new(10000.00m);

    [Fact(DisplayName = "Dadas seed e salt, quando Sample é chamado, então devolve os 52 primeiros bits big-endian de HMAC-SHA256(seed, salt)")]
    [Trait("Spec", Tag)]
    public void GivenSeedAndSaltWhenSampleThenFirst52BitsOfTheHmac()
    {
        var seed = MutationFixtures.Origin();
        var salt = MutationFixtures.SaltBytes();
        var digest = HMACSHA256.HashData(seed, salt);
        var expected = (long)(BinaryPrimitives.ReadUInt64BigEndian(digest) >> 12);

        var sample = CrashPointCalculator.Sample(new Seed(seed), new Salt(salt));

        Assert.Equal(expected, sample);
        Assert.InRange(sample, 0, E - 1);
    }

    [Fact(DisplayName = "Dadas seed, salt, RTP e cap, quando Compute é chamado, então é FromSample do Sample")]
    [Trait("Spec", Tag)]
    public void GivenInputsWhenComputeThenFromSampleOfSample()
    {
        var seed = new Seed(MutationFixtures.Origin());
        var salt = new Salt(MutationFixtures.SaltBytes());

        var crash = CrashPointCalculator.Compute(seed, salt, Rtp99, Cap);

        Assert.Equal(CrashPointCalculator.FromSample(CrashPointCalculator.Sample(seed, salt), Rtp99, Cap), crash);
    }

    [Fact(DisplayName = "Dada uma seed nula, quando Compute ou Sample é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullSeedWhenComputeOrSampleThenArgumentNull()
    {
        var salt = new Salt(MutationFixtures.SaltBytes());

        Assert.Equal("seed", Assert.Throws<ArgumentNullException>(() => CrashPointCalculator.Compute(null!, salt, Rtp99, Cap)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentNullException>(() => CrashPointCalculator.Sample(null!, salt)).ParamName);
    }

    [Fact(DisplayName = "Dada uma salt nula, quando Compute ou Sample é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullSaltWhenComputeOrSampleThenArgumentNull()
    {
        var seed = new Seed(MutationFixtures.Origin());

        Assert.Equal("salt", Assert.Throws<ArgumentNullException>(() => CrashPointCalculator.Compute(seed, null!, Rtp99, Cap)).ParamName);
        Assert.Equal("salt", Assert.Throws<ArgumentNullException>(() => CrashPointCalculator.Sample(seed, null!)).ParamName);
    }

    [Theory(DisplayName = "Dado um sample fora de [0, 2^52), quando FromSample é chamado, então lança ArgumentOutOfRangeException")]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(E)]
    [InlineData(E + 1)]
    [InlineData(long.MaxValue)]
    [Trait("Spec", Tag)]
    public void GivenSampleOutsideTheSpaceWhenFromSampleThenArgumentOutOfRange(long sample)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CrashPointCalculator.FromSample(sample, Rtp99, Cap));

        Assert.Equal("sample", exception.ParamName);
    }

    [Theory(DisplayName = "Dado o sample na borda inferior, quando FromSample é chamado, então o crash é 1.00 (piso)")]
    [InlineData(0.90)]
    [InlineData(0.99)]
    [Trait("Spec", Tag)]
    public void GivenSmallestSampleWhenFromSampleThenOneHundredFloor(double rtp)
    {
        Assert.Equal(new CrashPoint(1.00m), CrashPointCalculator.FromSample(0, new Rtp((decimal)rtp), Cap));
    }

    [Theory(DisplayName = "Dado um sample com E − X divisor exato, quando FromSample é chamado, então o crash é floor(100·RTP·E/(E−X))/100 exato")]
    [InlineData(2, 0.99, 1.98)]
    [InlineData(4, 0.99, 3.96)]
    [InlineData(2, 0.90, 1.80)]
    [InlineData(100, 0.97, 97.00)]
    [Trait("Spec", Tag)]
    public void GivenExactDivisorWhenFromSampleThenExactCrash(long divisor, double rtp, double expected)
    {
        var sample = E - (E / divisor);

        var crash = CrashPointCalculator.FromSample(sample, new Rtp((decimal)rtp), Cap);

        Assert.Equal(new CrashPoint((decimal)expected), crash);
    }

    [Fact(DisplayName = "Dado um sample logo abaixo de um valor exato, quando FromSample é chamado, então o crash é arredondado para baixo")]
    [Trait("Spec", Tag)]
    public void GivenSampleJustBelowAnExactValueWhenFromSampleThenFloored()
    {
        // 100 · 0.99 · E / (E/2 + 1) is just below 198: the floor is 197.
        Assert.Equal(new CrashPoint(1.97m), CrashPointCalculator.FromSample((E / 2) - 1, Rtp99, Cap));
        Assert.Equal(new CrashPoint(1.98m), CrashPointCalculator.FromSample(E / 2, Rtp99, Cap));
    }

    [Fact(DisplayName = "Dado o sample que leva o crash logo acima de 1.00, quando FromSample é chamado, então o crash é 1.01 e não 1.00")]
    [Trait("Spec", Tag)]
    public void GivenSampleJustAboveTheFloorWhenFromSampleThenOneOhOne()
    {
        // 100 · 0.99 · E / (E − X) = 101 exactly when E − X = 99·E/101; E is a power of two, so take the ceiling of X.
        var sample = E - (99 * E / 101);

        var crash = CrashPointCalculator.FromSample(sample, Rtp99, Cap);

        Assert.Equal(new CrashPoint(1.01m), crash);
        Assert.Equal(new CrashPoint(1.00m), CrashPointCalculator.FromSample(sample - 1, Rtp99, Cap));
    }

    [Theory(DisplayName = "Dado o maior sample, quando FromSample é chamado, então o crash é o cap (em centésimos, arredondado para baixo)")]
    [InlineData(10000.00, 10000.00)]
    [InlineData(2.505, 2.50)]
    [InlineData(1.00, 1.00)]
    [InlineData(1.009, 1.00)]
    [Trait("Spec", Tag)]
    public void GivenLargestSampleWhenFromSampleThenCap(double cap, double expected)
    {
        var crash = CrashPointCalculator.FromSample(E - 1, Rtp99, new Multiplier((decimal)cap));

        Assert.Equal(new CrashPoint((decimal)expected), crash);
    }

    [Fact(DisplayName = "Dado um cap cuja mantissa ocupa mais de 32 bits, quando o crash o atinge, então o crash é o cap inteiro")]
    [Trait("Spec", Tag)]
    public void GivenCapAbove32BitMantissaWhenReachedThenWholeCap()
    {
        // 2^32 + 100: the second 32-bit word of the decimal mantissa is 1.
        var cap = 4_294_967_396m;

        var crash = CrashPointCalculator.FromSample(E - 1, Rtp99, new Multiplier(cap));

        Assert.Equal(new CrashPoint(cap), crash);
    }

    [Fact(DisplayName = "Dado um cap cuja mantissa ocupa mais de 64 bits, quando o crash não o atinge, então o crash é o valor sem cap")]
    [Trait("Spec", Tag)]
    public void GivenCapAbove64BitMantissaWhenNotReachedThenUncappedCrash()
    {
        // 2^64 + 100: the third 32-bit word of the decimal mantissa is 1, the low 64 bits are 100.
        var cap = 18_446_744_073_709_551_716m;

        var crash = CrashPointCalculator.FromSample(E - 1, Rtp99, new Multiplier(cap));

        // floor(100 · 0.99 · E / 1) = 99 · E hundredths.
        Assert.Equal(new CrashPoint(99m * E / 100m), crash);
    }

    [Fact(DisplayName = "Dado um cap de 1.00, quando FromSample é chamado, então é aceito e o crash é sempre 1.00")]
    [Trait("Spec", Tag)]
    public void GivenCapOfOneWhenFromSampleThenAcceptedAndOne()
    {
        var cap = new Multiplier(1.00m);

        Assert.Equal(new CrashPoint(1.00m), CrashPointCalculator.FromSample(0, Rtp99, cap));
        Assert.Equal(new CrashPoint(1.00m), CrashPointCalculator.FromSample(E / 2, Rtp99, cap));
    }

    [Theory(DisplayName = "Dado um cap abaixo de 1.00, quando FromSample ou Compute é chamado, então lança InvalidCapException com mensagem")]
    [InlineData(0.99)]
    [InlineData(0.00)]
    [InlineData(-5.00)]
    [Trait("Spec", Tag)]
    public void GivenCapBelowOneWhenCalculatedThenInvalidCap(double cap)
    {
        var multiplier = new Multiplier((decimal)cap);

        var exception = Assert.Throws<InvalidCapException>(() => CrashPointCalculator.FromSample(E / 2, Rtp99, multiplier));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Throws<InvalidCapException>(
            () => CrashPointCalculator.Compute(new Seed(MutationFixtures.Origin()), new Salt(MutationFixtures.SaltBytes()), Rtp99, multiplier));
    }

    [Fact(DisplayName = "Dado um cap padrão (0), quando FromSample é chamado, então lança InvalidCapException")]
    [Trait("Spec", Tag)]
    public void GivenDefaultCapWhenFromSampleThenInvalidCap()
    {
        Assert.Throws<InvalidCapException>(() => CrashPointCalculator.FromSample(E - 1, Rtp99, default));
    }

    [Theory(DisplayName = "Dado um RTP padrão (fora de [0.90, 0.99]), quando FromSample é chamado, então lança InvalidRtpException com mensagem")]
    [InlineData(0L)]
    [InlineData(E / 2)]
    [InlineData(E - 1)]
    [Trait("Spec", Tag)]
    public void GivenDefaultRtpWhenFromSampleThenInvalidRtp(long sample)
    {
        var exception = Assert.Throws<InvalidRtpException>(() => CrashPointCalculator.FromSample(sample, default, Cap));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Theory(DisplayName = "Dado um RTP fora de [0.90, 0.99], quando o Rtp é criado, então lança InvalidRtpException")]
    [InlineData(0.89)]
    [InlineData(0.8999)]
    [InlineData(0.9901)]
    [InlineData(1.00)]
    [InlineData(0.00)]
    [Trait("Spec", Tag)]
    public void GivenRtpOutsideTheRangeWhenCreatedThenInvalidRtp(double rtp)
    {
        Assert.Throws<InvalidRtpException>(() => new Rtp((decimal)rtp));
    }

    [Theory(DisplayName = "Dado um RTP nas bordas 0.90 e 0.99, quando o crash é calculado, então é aceito")]
    [InlineData(0.90, 1.80)]
    [InlineData(0.99, 1.98)]
    [Trait("Spec", Tag)]
    public void GivenRtpOnTheEdgesWhenCalculatedThenAccepted(double rtp, double expected)
    {
        Assert.Equal(new CrashPoint((decimal)expected), CrashPointCalculator.FromSample(E / 2, new Rtp((decimal)rtp), Cap));
    }
}
