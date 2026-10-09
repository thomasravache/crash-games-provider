using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

public sealed class SalaConductorTests
{
    [Fact(DisplayName = "Dado crash 1.00, quando o voo inicia, então o condutor emite RoundCrashed sem RoundSync")]
    [Trait("Spec", "SPEC-0008:UT-03")]
    public async Task GivenInstantCrashWhenFlightStartsThenConductorEmitsCrashedWithoutSync()
    {
        using var harness = new ConductorHarness(clock => new ScriptedFairness(clock, 1.00m, 2.00m));

        await harness.RunUntilAsync(() => harness.Broadcaster.Of<RoundCrashedMessage>().Count >= 1, "the RoundCrashed of round 1");

        var betting = Assert.Single(harness.Broadcaster.Of<RoundBettingMessage>(), m => m.RoundNumber == 1);
        var flying = Assert.Single(harness.Broadcaster.Of<RoundFlyingMessage>(), m => m.RoundId == betting.RoundId);
        var crashed = Assert.Single(harness.Broadcaster.Of<RoundCrashedMessage>(), m => m.RoundId == betting.RoundId);
        var messages = harness.Broadcaster.Describe();

        Assert.True(crashed.CrashPoint.Value == 1.00m, $"Expected the crash point 1.00. Messages:{Environment.NewLine}{messages}");
        Assert.True(
            crashed.CrashedAt == flying.StartedAt,
            $"An instant crash ends the flight at its start ({flying.StartedAt:O}), not at {crashed.CrashedAt:O}.");
        Assert.True(
            !harness.Broadcaster.Of<RoundSyncMessage>().Any(m => m.RoundId == betting.RoundId),
            $"A round that crashes at 1.00 has no RoundSync. Messages:{Environment.NewLine}{messages}");

        var order = harness.Broadcaster.Events.Select(e => e.Message).ToList();
        Assert.True(
            order.IndexOf(flying) < order.IndexOf(crashed),
            $"RoundFlying must precede RoundCrashed. Messages:{Environment.NewLine}{messages}");
    }

    // From the G4 review of SPEC-0008: a reveal whose crash point differs from the one read at the start of the flight is
    // never published as a normal crash; the round is annulled with the reason CRASH_POINT_MISMATCH.
    [Fact(DisplayName = "Dado o crashPoint revelado diferente do lido no início do voo, quando a rodada encerra, então o condutor não publica RoundCrashed e anula a rodada com motivo CRASH_POINT_MISMATCH")]
    [Trait("Spec", "SPEC-0008:UT-03")]
    public async Task GivenRevealedCrashPointDiffersWhenRoundEndsThenConductorAnnulsWithCrashPointMismatch()
    {
        using var harness = new ConductorHarness(clock => new MismatchedRevealFairness(new ScriptedFairness(clock, 1.50m, 2.00m)));

        await harness.RunUntilAsync(
            () => harness.Broadcaster.Of<RoundCrashedMessage>().Count + harness.Broadcaster.Of<RoundAnnulledMessage>().Count >= 1,
            "the end of round 1");

        var betting = Assert.Single(harness.Broadcaster.Of<RoundBettingMessage>(), m => m.RoundNumber == 1);
        var messages = harness.Broadcaster.Describe();
        Assert.True(
            !harness.Broadcaster.Of<RoundCrashedMessage>().Any(m => m.RoundId == betting.RoundId),
            $"A round whose revealed crash point diverges must not be published as crashed. Messages:{Environment.NewLine}{messages}");
        var annulled = Assert.Single(harness.Broadcaster.Of<RoundAnnulledMessage>(), m => m.RoundId == betting.RoundId);
        Assert.Equal("CRASH_POINT_MISMATCH", annulled.Reason);

        var stored = await harness.Store.FindAsync(new RoundId(betting.RoundId), TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(RoundState.Annulled, stored.State);
        Assert.Equal("CRASH_POINT_MISMATCH", stored.AnnulReason);
        Assert.Null(stored.CrashPoint);
    }

    [Fact(DisplayName = "Dado FakeTimeProvider em t = 5.000 ms de voo, quando o sync é emitido, então o multiplicador é floor(e^(0,3)·100)/100 = 1.34")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenFlightAtFiveSecondsWhenSyncIsEmittedThenMultiplierIsOneThirtyFour()
    {
        using var harness = new ConductorHarness(clock => new ScriptedFairness(clock, 10.00m));
        var at5s = TimeSpan.FromMilliseconds(5_000);

        await harness.RunUntilAsync(
            () => harness.Broadcaster.Of<RoundFlyingMessage>() is [var flying]
                && harness.Broadcaster.Of<RoundSyncMessage>().Any(s => s.ServerTime - flying.StartedAt >= at5s),
            "a RoundSync at 5,000 ms of flight");

        var flying = Assert.Single(harness.Broadcaster.Of<RoundFlyingMessage>());
        var syncs = harness.Broadcaster.Of<RoundSyncMessage>().Where(s => s.RoundId == flying.RoundId).ToList();
        var messages = harness.Broadcaster.Describe();

        Assert.True(
            string.Equals(flying.GrowthK, "0.00006", StringComparison.Ordinal),
            $"RoundFlying carries growthK as the decimal string \"0.00006\", got \"{flying.GrowthK}\".");
        var at5000 = syncs.SingleOrDefault(s => s.ServerTime - flying.StartedAt == at5s);
        Assert.True(at5000 is not null, $"Syncs fall on startedAt + n·200 ms; expected one at 5,000 ms. Messages:{Environment.NewLine}{messages}");
        Assert.Equal(1.34m, at5000.Multiplier.Value);

        // Every sync carries m(t) at its server time and they are 200 ms apart.
        foreach (var sync in syncs)
        {
            var elapsed = (sync.ServerTime - flying.StartedAt).TotalMilliseconds;
            var expected = Math.Floor((decimal)Math.Exp(0.00006 * elapsed) * 100m) / 100m;
            Assert.True(
                sync.Multiplier.Value == expected,
                $"At {elapsed} ms of flight the sync must carry {expected}, got {sync.Multiplier.Value}.");
            Assert.True(elapsed % 200 == 0, $"Sync at {elapsed} ms is off the 200 ms grid. Messages:{Environment.NewLine}{messages}");
        }

        Assert.True(syncs.Count >= 25, $"Expected the 25 syncs of the first 5 s of flight, got {syncs.Count}.");
    }

    /// <summary>The scripted chain, except that <c>RevealAsync</c> returns a crash point 1.00 above the one read at flight start.</summary>
    private sealed class MismatchedRevealFairness(ScriptedFairness inner) : IFairnessService
    {
        public Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct) => inner.CreateChainAsync(sala, length, ct);

        public Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct) => inner.NextRoundAsync(sala, ct);

        public Task<CrashPoint> GetCrashPointAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct) =>
            inner.GetCrashPointAsync(chain, roundIndex, rtp, cap, ct);

        public async Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
        {
            var revealed = await inner.RevealAsync(chain, roundIndex, rtp, cap, ct).ConfigureAwait(false);
            return revealed with { CrashPoint = new CrashPoint(revealed.CrashPoint.Value + 1.00m) };
        }

        public CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap) => inner.ComputeCrashPoint(seed, salt, rtp, cap);

        public VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed) =>
            inner.Verify(seed, previousSeed, salt, rtp, cap, claimed);
    }
}
