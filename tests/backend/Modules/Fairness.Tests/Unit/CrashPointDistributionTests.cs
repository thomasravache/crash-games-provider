using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.TestKit.Randomness;
using Xunit;

namespace Apogeu.Fairness.Tests.Unit;

public sealed class CrashPointDistributionTests
{
    [Fact(DisplayName = "Dado 1.000.000 de rodadas a partir de uma cadeia com origem fixa, quando medida a fração com crash ≥ 2,00, então fica em RTP/2 ± 0,3 p.p.")]
    [Trait("Spec", "SPEC-0007:UT-06")]
    public void GivenOneMillionRoundsWhenShareAtOrAboveTwoIsMeasuredThenItIsHalfTheRtp()
    {
        const int Rounds = 1_000_000;
        const decimal RtpValue = 0.97m;
        const decimal Tolerance = 0.003m;
        var origin = new DeterministicSeedSource("SPEC-0007:UT-06").NextSeed();
        var salt = new Salt(new DeterministicSeedSource("SPEC-0007:UT-06:salt").NextSeed());
        var rtp = new Rtp(RtpValue);
        var cap = new Multiplier(10000.00m);

        // The rounds of a chain of N = 1,000,000 links use h_0..h_{N-1}; the order does not change the share.
        var link = origin;
        var atOrAboveTwo = 0;
        for (var i = 0; i < Rounds; i++)
        {
            if (CrashPointCalculator.Compute(new Seed(link), salt, rtp, cap).Value >= 2.00m)
            {
                atOrAboveTwo++;
            }

            link = SHA256.HashData(link);
        }

        var share = (decimal)atOrAboveTwo / Rounds;
        var expected = RtpValue / 2;
        Assert.True(
            Math.Abs(share - expected) <= Tolerance,
            $"Share of crashes ≥ 2.00x = {share:P3}, expected {expected:P3} ± {Tolerance:P1}.");
    }
}
