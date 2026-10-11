using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.CashOut;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

/// <summary>
/// The leader side of the cash-out (SPEC-0023 §6) on the real <c>SalaConductor</c> driven by a <c>FakeTimeProvider</c>
/// (<see cref="ConductorHarness"/>): k = 0,00006/ms and a scripted chain whose round 1 crashes at 2,00x, so
/// t_c = ln 2/k ≈ 11.552,45 ms, not rounded.
/// </summary>
public sealed class CashOutArbiterTests
{
    private const string SalaId = "demo";

    /// <summary>A lowercase hex of 32 bytes (commitment and seed of the persisted rounds; never verified here).</summary>
    private static readonly string Commitment = new('a', 64);

    private static readonly GrowthCurve Curve = new(GrowthCurve.DefaultK);

    [Fact(DisplayName = "Dados k = 0,00006, crash 2,00 e FakeTimeProvider, quando o cash-out é recebido em 5.000 ms, um tick antes de t_c e exatamente em t_c, então Accepted 1,34x, Accepted 1,99x e TooLate")]
    [Trait("Spec", "SPEC-0023:UT-01")]
    public async Task GivenCrashTwoWhenCashOutIsReceivedAtFiveSecondsJustBeforeAndAtCrashThenAcceptedAcceptedAndTooLate()
    {
        using var harness = new ConductorHarness(clock => new ScriptedFairness(clock, 2.00m, 2.00m));
        var sink = new RecordingAutoCashOutSink(harness.Clock);
        var arbiter = new LeaderCashOutArbiter(harness.Conductor, sink);
        var flying = await FlyingAsync(harness);
        var roundId = new RoundId(flying.RoundId);
        var crashAt = flying.StartedAt + Curve.TimeToCrash(2.00m);

        var atFiveSeconds = arbiter.Decide(roundId, flying.StartedAt + TimeSpan.FromMilliseconds(5_000));
        var justBefore = arbiter.Decide(roundId, crashAt - TimeSpan.FromTicks(1));
        var atCrash = arbiter.Decide(roundId, crashAt);

        // floor(e^(0,00006·5000)·100)/100 = floor(134,98…)/100.
        Assert.Equal(new Multiplier(1.34m), Assert.IsType<CashOutAccepted>(atFiveSeconds).Multiplier);

        // t_c is not rounded: one tick before it the flight is still on, at 1,99x displayed.
        Assert.Equal(new Multiplier(1.99m), Assert.IsType<CashOutAccepted>(justBefore).Multiplier);

        // Latency tolerance 0: received exactly at startedAt + t_c is too late.
        Assert.IsType<CashOutTooLate>(atCrash);
    }

    [Fact(DisplayName = "Dados alvo 1,50x e alvo 3,00x com crash 2,00x, quando o agendamento roda, então o primeiro executa em t_alvo com 1,50x exatos e o segundo não executa")]
    [Trait("Spec", "SPEC-0023:UT-03")]
    public async Task GivenTargetsBelowAndAboveCrashWhenScheduleRunsThenOnlyTheFirstExecutesAtItsTarget()
    {
        using var harness = new ConductorHarness(clock => new ScriptedFairness(clock, 2.00m, 2.00m));
        var sink = new RecordingAutoCashOutSink(harness.Clock);
        var arbiter = new LeaderCashOutArbiter(harness.Conductor, sink);
        var flying = await FlyingAsync(harness);
        var roundId = new RoundId(flying.RoundId);
        var below = Guid.NewGuid();
        var above = Guid.NewGuid();

        arbiter.ScheduleAuto(roundId, below, new Multiplier(1.50m));
        arbiter.ScheduleAuto(roundId, above, new Multiplier(3.00m));
        await harness.RunUntilAsync(
            () => harness.Broadcaster.Of<RoundCrashedMessage>().Any(c => c.RoundId == flying.RoundId),
            "the crash of round 1");

        var executions = sink.Executed;
        var describe = $"Executed: [{string.Join(", ", executions)}]. Messages:{Environment.NewLine}{harness.Broadcaster.Describe()}";
        var executed = Assert.Single(executions, e => e.Executed.BetId == below);
        Assert.True(!executions.Any(e => e.Executed.BetId == above), $"A target above the crash never executes. {describe}");

        // t_alvo = ln(1,5)/k ≈ 6.757,85 ms: executed then (the conductor's clock), not at the next sync or at the crash.
        var target = flying.StartedAt + TimeSpan.FromMilliseconds(Math.Log(1.5) / GrowthCurve.DefaultK);
        Assert.Equal(new Multiplier(1.50m), executed.Executed.Multiplier);
        Assert.Equal(SalaId, executed.Executed.SalaId);
        Assert.Equal(roundId, executed.Executed.RoundId);
        Assert.True(
            (executed.Executed.ExecutedAt - target).Duration() < TimeSpan.FromMilliseconds(1),
            $"executedAt {executed.Executed.ExecutedAt:O} is t_alvo {target:O}. {describe}");
        Assert.True(
            (executed.ClockAt - target).Duration() < TimeSpan.FromMilliseconds(1),
            $"The leader executes at t_alvo ({target:O}); its clock was at {executed.ClockAt:O}. {describe}");
    }


    [Fact(DisplayName = "Dado o árbitro do líder sem o voo em memória, quando a rodada não está encerrada (ou é desconhecida), então Undecided; quando está encerrada, então decide pelos dados persistidos (startedAt e ponto do crash) com a mesma regra")]
    [Trait("Spec", "SPEC-0023:UT-06")]
    public async Task GivenLeaderWithoutFlightInMemoryWhenRoundIsNotEndedThenUndecidedAndWhenEndedThenDecidedFromPersistedData()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new ConductorHarness(clock => new ScriptedFairness(clock, 2.00m));
        var arbiter = new LeaderCashOutArbiter(harness.Conductor, new RecordingAutoCashOutSink(harness.Clock));
        var startedAt = harness.Clock.GetUtcNow().AddSeconds(-30);
        var crashAt = startedAt + Curve.TimeToCrash(2.00m);
        var parameters = new RoundParameters(0.97m, 10_000.00m, GrowthCurve.DefaultK);

        // A round another leader flies (persisted, Flying) and one it crashed at 2,00x: neither is in this conductor's memory.
        var flying = Round.OpenBetting(new RoundId(Guid.CreateVersion7()), SalaId, 2, Guid.NewGuid(), 2, Commitment, startedAt, parameters);
        flying.StartFlight(startedAt.AddSeconds(20));
        await harness.Store.SaveAsync(flying, ct);
        var crashed = Round.Restore(
            new RoundId(Guid.CreateVersion7()), SalaId, 1, Guid.NewGuid(), 1, Commitment, RoundState.Crashed, startedAt,
            startedAt, crashAt, 2.00m, Commitment, annulReason: null, annulledAt: null, parameters);
        await harness.Store.SaveAsync(crashed, ct);
        var unknown = new RoundId(Guid.CreateVersion7());

        // Not ended (or unknown): the leader cannot tell, so it does not decide — never TooLate (review G4, M2).
        Assert.IsType<CashOutUndecided>(arbiter.Decide(flying.Id, startedAt.AddSeconds(21)));
        Assert.IsType<CashOutUndecided>(await DecideAsync(arbiter, flying.Id, startedAt.AddSeconds(21), ct));
        Assert.IsType<CashOutUndecided>(await DecideAsync(arbiter, unknown, startedAt.AddSeconds(21), ct));

        // Ended: the same rule from the public data persisted with the round.
        Assert.Equal(new Multiplier(1.34m), Assert.IsType<CashOutAccepted>(await DecideAsync(arbiter, crashed.Id, startedAt.AddMilliseconds(5_000), ct)).Multiplier);
        Assert.Equal(new Multiplier(1.99m), Assert.IsType<CashOutAccepted>(await DecideAsync(arbiter, crashed.Id, crashAt - TimeSpan.FromTicks(1), ct)).Multiplier);
        Assert.IsType<CashOutTooLate>(await DecideAsync(arbiter, crashed.Id, crashAt, ct));
    }

    /// <summary>
    /// Seam of review G4 (M2): <c>Task&lt;CashOutDecision&gt; LeaderCashOutArbiter.DecideAsync(RoundId, DateTimeOffset,
    /// CancellationToken)</c> — the flight in memory first, then the round as persisted (bound by reflection so the suite
    /// compiles before it exists).
    /// </summary>
    private static async Task<CashOutDecision> DecideAsync(LeaderCashOutArbiter arbiter, RoundId roundId, DateTimeOffset receivedAt, CancellationToken ct)
    {
        var method = typeof(LeaderCashOutArbiter).GetMethod("DecideAsync", [typeof(RoundId), typeof(DateTimeOffset), typeof(CancellationToken)]);
        Assert.True(
            method is not null && method.ReturnType == typeof(Task<CashOutDecision>),
            "LeaderCashOutArbiter has no Task<CashOutDecision> DecideAsync(RoundId, DateTimeOffset, CancellationToken): the leader cannot decide a round out of its memory from the persisted data.");
        return await ((Task<CashOutDecision>)method.Invoke(arbiter, [roundId, receivedAt, ct])!).ConfigureAwait(false);
    }

    private static async Task<RoundFlyingMessage> FlyingAsync(ConductorHarness harness)
    {
        await harness.RunUntilAsync(() => harness.Broadcaster.Of<RoundFlyingMessage>().Count >= 1, "the flight of round 1").ConfigureAwait(false);
        return harness.Broadcaster.Of<RoundFlyingMessage>()[0];
    }

    /// <summary>Records each automatic cash-out the leader executes, with the conductor's clock when it was published.</summary>
    private sealed class RecordingAutoCashOutSink(TimeProvider clock) : IAutoCashOutSink
    {
        private readonly Lock _gate = new();
        private readonly List<(AutoCashOutExecuted Executed, DateTimeOffset ClockAt)> _executed = [];

        public IReadOnlyList<(AutoCashOutExecuted Executed, DateTimeOffset ClockAt)> Executed
        {
            get
            {
                lock (_gate)
                {
                    return [.. _executed];
                }
            }
        }

        public Task PublishAsync(AutoCashOutExecuted executed, CancellationToken ct)
        {
            lock (_gate)
            {
                _executed.Add((executed, clock.GetUtcNow()));
            }

            return Task.CompletedTask;
        }
    }
}
