using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.TestKit.Randomness;
using Xunit;

namespace Apogeu.Fairness.Tests.Integration;

public sealed class RtpSimulationTests
{
    [Fact(DisplayName = "Dados 10 milhões de rodadas simuladas com RTP 0,97, quando medido o retorno de um cash-out fixo em 2,00x, então fica em 0,97 ± 0,001")]
    [Trait("Spec", "SPEC-0007:IT-05")]
    [Trait("Category", "Nightly")]
    public void GivenTenMillionRoundsWhenFixedCashOutReturnIsMeasuredThenItMatchesTheRtp()
    {
        const int Rounds = 10_000_000;
        const decimal RtpValue = 0.97m;
        const decimal CashOut = 2.00m;
        const decimal Tolerance = 0.001m;
        var link = new DeterministicSeedSource("SPEC-0007:IT-05").NextSeed();
        var salt = new Salt(new DeterministicSeedSource("SPEC-0007:IT-05:salt").NextSeed());
        var rtp = new Rtp(RtpValue);
        var cap = new Multiplier(10000.00m);

        // One unit staked per round, cashed out at 2.00x whenever the round reaches it.
        long winningRounds = 0;
        for (var i = 0; i < Rounds; i++)
        {
            if (CrashPointCalculator.Compute(new Seed(link), salt, rtp, cap).Value >= CashOut)
            {
                winningRounds++;
            }

            link = SHA256.HashData(link);
        }

        var measuredRtp = CashOut * winningRounds / Rounds;
        Assert.True(
            Math.Abs(measuredRtp - RtpValue) <= Tolerance,
            $"Measured RTP {measuredRtp:F5} over {Rounds:N0} rounds, expected {RtpValue} ± {Tolerance}.");
    }
}
