using System.Globalization;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Randomness;
using Xunit;

namespace Apogeu.Fairness.Tests.Unit;

public sealed class CrashPointCalculatorTests
{
    private const long SampleSpace = 1L << 52;
    private static readonly Multiplier DefaultCap = new(10000.00m);

    public static TheoryData<string, string, string, string, string, string, string> Vectors => FairnessVectors.Rows();

    [Theory(DisplayName = "Dados os vetores de fairness-v1.json, quando ComputeCrashPoint é chamado, então cada resultado é idêntico ao vetor")]
    [MemberData(nameof(Vectors))]
    [Trait("Spec", "SPEC-0007:UT-01")]
    public void GivenPublishedVectorWhenComputedThenResultEqualsVector(
        string description, string seed, string previousSeed, string salt, string rtp, string cap, string crashPoint)
    {
        Assert.NotNull(description);
        Assert.NotNull(previousSeed);

        var result = CrashPointCalculator.Compute(Seed.FromHex(seed), Salt.FromHex(salt), new Rtp(Parse(rtp)), new Multiplier(Parse(cap)));

        Assert.Equal(Parse(crashPoint), result.Value);
        Assert.Equal(crashPoint, result.Value.ToString("0.00", CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "Dados os vetores de fairness-v1.json, quando inspecionados, então incluem um crash instantâneo de 1.00x e um no limite")]
    [Trait("Spec", "SPEC-0007:UT-01")]
    public void GivenPublishedVectorsWhenInspectedThenTheyIncludeInstantCrashAndCap()
    {
        var vectors = FairnessVectors.All();

        Assert.True(vectors.Count >= 6, $"Expected at least 6 vectors, found {vectors.Count}.");
        Assert.Contains(vectors, vector => vector.CrashPointValue == 1.00m);
        Assert.Contains(vectors, vector => vector.CrashPointValue == vector.CapValue);
    }

    [Theory(DisplayName = "Dado X tal que o valor da fórmula fica abaixo de 1,00, quando calculado, então o crash é 1.00")]
    [InlineData(0L, "0.97")]
    [InlineData(0L, "0.90")]
    [InlineData(100_000_000_000_000L, "0.97")]
    [Trait("Spec", "SPEC-0007:UT-01")]
    public void GivenSampleBelowOneWhenComputedThenCrashIsOne(long sample, string rtp)
    {
        // 100 · RTP · E / (E − X) < 100 for these samples, so the formula alone would give less than 1.00x.
        Assert.True(ReferenceAlgorithm.CrashPoint(sample, Parse(rtp), 10000.00m) == 1.00m, "The sample does not fall below 1.00x.");

        var result = CrashPointCalculator.FromSample(sample, new Rtp(Parse(rtp)), DefaultCap);

        Assert.Equal(1.00m, result.Value);
    }

    [Theory(DisplayName = "Dados X = E − 1 e limite 10.000,00, quando calculado, então o resultado é 10000.00")]
    [InlineData("0.90")]
    [InlineData("0.97")]
    [InlineData("0.99")]
    [Trait("Spec", "SPEC-0007:UT-02")]
    public void GivenSampleNextToSampleSpaceWhenComputedThenCrashIsCap(string rtp)
    {
        var result = CrashPointCalculator.FromSample(SampleSpace - 1, new Rtp(Parse(rtp)), DefaultCap);

        Assert.Equal(10000.00m, result.Value);
    }

    [Fact(DisplayName = "Dado RTP 0,85, quando validado, então lança InvalidRtp")]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public void GivenRtpBelowRangeWhenValidatedThenInvalidRtp()
    {
        var source = new DeterministicSeedSource("SPEC-0007:UT-05");

        Assert.Throws<InvalidRtpException>(() =>
            CrashPointCalculator.Compute(new Seed(source.NextSeed()), new Salt(source.NextSeed()), new Rtp(0.85m), DefaultCap));
    }

    [Fact(DisplayName = "Dado RTP 1,00, quando validado, então lança InvalidRtp")]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public void GivenRtpAboveRangeWhenValidatedThenInvalidRtp()
    {
        var source = new DeterministicSeedSource("SPEC-0007:UT-05");

        Assert.Throws<InvalidRtpException>(() =>
            CrashPointCalculator.Compute(new Seed(source.NextSeed()), new Salt(source.NextSeed()), new Rtp(1.00m), DefaultCap));
    }

    [Fact(DisplayName = "Dados RTP nos extremos 0,90 e 0,99, quando validados, então são aceitos")]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public void GivenRtpAtRangeBoundsWhenValidatedThenAccepted()
    {
        Assert.Equal(0.90m, new Rtp(0.90m).Value);
        Assert.Equal(0.99m, new Rtp(0.99m).Value);
    }

    [Fact(DisplayName = "Dado limite 0,5, quando validado, então lança InvalidCap")]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public void GivenCapBelowOneWhenValidatedThenInvalidCap()
    {
        var source = new DeterministicSeedSource("SPEC-0007:UT-05");

        Assert.Throws<InvalidCapException>(() =>
            CrashPointCalculator.Compute(new Seed(source.NextSeed()), new Salt(source.NextSeed()), new Rtp(0.97m), new Multiplier(0.5m)));
    }

    [Fact(DisplayName = "Dada uma semente de 31 bytes, quando validada, então lança InvalidSeed")]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public void GivenSeedOf31BytesWhenValidatedThenInvalidSeed()
    {
        var bytes = new DeterministicSeedSource("SPEC-0007:UT-05").NextSeed()[..31];

        Assert.Throws<InvalidSeedException>(() => new Seed(bytes));
    }

    private static decimal Parse(string value) => decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
