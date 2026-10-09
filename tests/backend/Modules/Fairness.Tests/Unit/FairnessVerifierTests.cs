using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Randomness;
using Xunit;

namespace Apogeu.Fairness.Tests.Unit;

public sealed class FairnessVerifierTests
{
    private const int ChainLength = 10;
    private const decimal RtpValue = 0.97m;
    private const decimal CapValue = 10000.00m;

    [Theory(DisplayName = "Dada uma cadeia curta gerada no teste, quando Verify recebe s_r e s_{r-1} corretos e o crash correto, então retorna Valid")]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(ChainLength)]
    [Trait("Spec", "SPEC-0007:UT-03")]
    public void GivenRealChainLinksAndCorrectCrashWhenVerifiedThenValid(int round)
    {
        var chain = ShortChain(out var salt);
        var (seed, previousSeed) = Round(chain, round);
        var crash = ReferenceAlgorithm.CrashPoint(seed, salt, RtpValue, CapValue);

        var result = FairnessVerifier.Verify(
            new Seed(seed), new Seed(previousSeed), new Salt(salt), new Rtp(RtpValue), new Multiplier(CapValue), new CrashPoint(crash));

        Assert.Equal(VerificationResult.Valid, result);
    }

    [Theory(DisplayName = "Dada uma semente com um byte alterado, quando verificada, então Verify retorna ChainMismatch")]
    [InlineData(0)]
    [InlineData(31)]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public void GivenSeedWithOneAlteredByteWhenVerifiedThenChainMismatch(int alteredByte)
    {
        var chain = ShortChain(out var salt);
        var (seed, previousSeed) = Round(chain, 4);
        var tampered = (byte[])seed.Clone();
        tampered[alteredByte] ^= 0x01;
        var crashOfTampered = ReferenceAlgorithm.CrashPoint(tampered, salt, RtpValue, CapValue);

        var result = FairnessVerifier.Verify(
            new Seed(tampered), new Seed(previousSeed), new Salt(salt), new Rtp(RtpValue), new Multiplier(CapValue), new CrashPoint(crashOfTampered));

        Assert.Equal(VerificationResult.ChainMismatch, result);
    }

    [Fact(DisplayName = "Dado um crash informado diferente do calculado, quando verificado, então Verify retorna CrashPointMismatch")]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public void GivenDifferentClaimedCrashWhenVerifiedThenCrashPointMismatch()
    {
        var chain = ShortChain(out var salt);
        var (seed, previousSeed) = Round(chain, 4);
        var crash = ReferenceAlgorithm.CrashPoint(seed, salt, RtpValue, CapValue);
        var claimed = crash == CapValue ? crash - 0.01m : crash + 0.01m;

        var result = FairnessVerifier.Verify(
            new Seed(seed), new Seed(previousSeed), new Salt(salt), new Rtp(RtpValue), new Multiplier(CapValue), new CrashPoint(claimed));

        Assert.Equal(VerificationResult.CrashPointMismatch, result);
    }

    /// <summary>h_0..h_N from a fixed origin; the salt is fixed too.</summary>
    private static byte[][] ShortChain(out byte[] salt)
    {
        var source = new DeterministicSeedSource("SPEC-0007:UT-03");
        var origin = source.NextSeed();
        salt = source.NextSeed();
        return ReferenceAlgorithm.Chain(origin, ChainLength);
    }

    /// <summary>Round r uses s_r = h_{N-r}; the previous seed is s_{r-1} = h_{N-r+1} (s_0 = h_N).</summary>
    private static (byte[] Seed, byte[] PreviousSeed) Round(byte[][] chain, int round) =>
        (chain[ChainLength - round], chain[ChainLength - round + 1]);
}
