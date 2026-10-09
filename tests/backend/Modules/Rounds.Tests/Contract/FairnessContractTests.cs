using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Contract;

public sealed class FairnessContractTests
{
    private static readonly Rtp DemoRtp = new(0.97m);
    private static readonly Multiplier DemoCap = new(10000.00m);

    [Fact(DisplayName = "Dado o contrato SPEC-0007 e os vetores, quando o motor usa NextRoundAsync/GetCrashPointAsync/RevealAsync, então o ponto de crash publicado bate com ComputeCrashPoint e RevealAsync só é chamado depois do crash")]
    [Trait("Spec", "SPEC-0008:CT-02")]
    public async Task GivenFairnessContractAndVectorsWhenEngineRunsThenPublishedCrashMatchesComputeAndRevealFollowsCrash()
    {
        var vectors = FairnessVectors.All();
        VectorFairness? fairness = null;
        using var harness = new ConductorHarness(clock => fairness = new VectorFairness(clock, vectors));

        await harness.RunUntilAsync(
            () => harness.Broadcaster.Of<RoundCrashedMessage>().Count >= vectors.Count,
            $"the RoundCrashed of the {vectors.Count} vector rounds",
            maxSteps: 50_000);

        var calls = fairness!.Calls;
        var bettings = harness.Broadcaster.Of<RoundBettingMessage>();
        var flights = harness.Broadcaster.Of<RoundFlyingMessage>();
        var crashes = harness.Broadcaster.Of<RoundCrashedMessage>();
        var log = $"Messages:{Environment.NewLine}{harness.Broadcaster.Describe()}{Environment.NewLine}Calls:{Environment.NewLine}{string.Join(Environment.NewLine, calls)}";

        for (var r = 1; r <= vectors.Count; r++)
        {
            var vector = vectors[r - 1];
            var betting = Assert.Single(bettings, m => m.RoundNumber == r);
            var flying = Assert.Single(flights, m => m.RoundId == betting.RoundId);
            var crashed = Assert.Single(crashes, m => m.RoundId == betting.RoundId);
            var seed = Seed.FromHex(vector.Seed);
            var previous = Seed.FromHex(vector.PreviousSeed);
            var salt = Salt.FromHex(vector.Salt);
            var expected = CrashPointCalculator.Compute(seed, salt, DemoRtp, DemoCap);

            // The commitment published before the bets is SHA256(s_r), the previous seed of the vector.
            Assert.Equal(vector.PreviousSeed, betting.Commitment);
            Assert.True(crashed.CrashPoint.Value == expected.Value, $"Round {r}: published {crashed.CrashPoint.Value}, ComputeCrashPoint {expected.Value}. {log}");
            Assert.Equal(vector.Seed, crashed.RevealedSeed);
            Assert.Equal(
                VerificationResult.Valid,
                FairnessVerifier.Verify(seed, previous, salt, DemoRtp, DemoCap, new CrashPoint(crashed.CrashPoint.Value)));
            if (vector.RtpValue == DemoRtp.Value && vector.CapValue == DemoCap.Value)
            {
                Assert.Equal(vector.CrashPointValue, crashed.CrashPoint.Value);
            }

            var crashPointCalls = calls.Where(c => string.Equals(c.Method, "GetCrashPointAsync", StringComparison.Ordinal) && c.RoundIndex == r).ToList();
            Assert.True(crashPointCalls.Count > 0, $"Round {r}: the flight must get its crash point with GetCrashPointAsync. {log}");
            Assert.True(
                crashPointCalls.All(c => c.At >= betting.BettingEndsAt && c.At <= flying.StartedAt),
                $"Round {r}: GetCrashPointAsync is called at the start of the flight ({flying.StartedAt:O}). {log}");

            var reveals = calls.Where(c => string.Equals(c.Method, "RevealAsync", StringComparison.Ordinal) && c.RoundIndex == r).ToList();
            Assert.True(reveals.Count > 0, $"Round {r}: the seed must be revealed with RevealAsync after the crash. {log}");
            Assert.True(
                reveals.All(c => c.At >= crashed.CrashedAt),
                $"Round {r}: RevealAsync must only be called after the crash at {crashed.CrashedAt:O}. {log}");
        }
    }
}
