using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// Cash-out with two instances of the Host on the same PostgreSQL and Redis (SPEC-0023 §4): only the leader of the room knows
/// the crash, so the instance that is not the leader forwards the decision to it. Each instance's
/// <see cref="ICashOutArbiter"/> is observed by an <see cref="ObservedCashOutArbiter"/>.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class MultiInstanceCashOutTests(BettingContainers containers)
{
    private const decimal CrashPoint = 2.00m;
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dadas duas instâncias do Host com Redis real e o líder na instância A, quando o cash-out chega na instância B, então é decidido pelo líder com a mesma regra")]
    [Trait("Spec", "SPEC-0023:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenTwoInstancesWhenCashOutReachesFollowerThenLeaderDecidesWithSameRule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await Cluster.StartAsync(containers, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var rounds = await RoundWatch.StartAsync(cluster.Leader, ct);
        await using var bets = await BetsClient.ConnectAsync(cluster.Follower, player, ct);

        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(cluster.Follower, rounds, async () =>
        [
            BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await bets.PlaceAsync(panel: 2, amount: "10.00", ct).ConfigureAwait(false)),
        ], ct);

        await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
        var sentAt = DateTimeOffset.UtcNow;
        var outcome = await bets.CashOutAsync(placed[0], ct);
        var answeredAt = DateTimeOffset.UtcNow;

        var cashedOut = CashOutFlight.CashedOutOf(outcome);
        flight.AssertReceivedBetween(Json.Decimal(cashedOut, "multiplier"), sentAt, answeredAt, outcome.ToString());
        Assert.Equal(CashOutMath.Payout(10.00m, Json.Decimal(cashedOut, "multiplier")), Json.Decimal(cashedOut, "payout"));

        // The decision ran on the leader (the only instance that knows the crash), for the instant of reception.
        var decided = cluster.LeaderProbe.Calls.Where(c => c.Leading && c.RoundId.Value == flight.RoundId).ToList();
        Assert.True(
            decided.Count == 1 && decided[0].Decision is CashOutAccepted && decided[0].ReceivedAt >= sentAt.AddMilliseconds(-1) && decided[0].ReceivedAt <= answeredAt,
            $"Expected one decision on the leader for a reception between {sentAt:O} and {answeredAt:O}. Leader: [{cluster.LeaderProbe.Describe()}]; follower: [{cluster.FollowerProbe.Describe()}]");
        Assert.DoesNotContain(cluster.FollowerProbe.Calls, c => c.Leading);

        // After the crash the same rule, decided by the leader: too late.
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        var tooLate = await bets.CashOutAsync(placed[1], ct);
        Assert.True(tooLate.FailedWith("CASHOUT_TOO_LATE"), $"Expected CASHOUT_TOO_LATE after the crash, got {tooLate}");
    }

    [Fact(DisplayName = "Dado o líder impedido de responder além do prazo de 200 ms, quando o cash-out chega numa instância que não é o líder, então CashOutPending e, ao liberar o líder, CashedOut com o multiplicador do horário de recebimento gravado (ou CashOutRejected se recebido depois do crash)")]
    [Trait("Spec", "SPEC-0023:IT-07")]
    [Trait("Category", "Integration")]
    public async Task GivenLeaderNotAnsweringWhenCashOutReachesFollowerThenPendingAndDecidedLaterByRecordedReception()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await Cluster.StartAsync(containers, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var rounds = await RoundWatch.StartAsync(cluster.Leader, ct);
        await using var bets = await BetsClient.ConnectAsync(cluster.Follower, player, ct);

        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(cluster.Follower, rounds, async () =>
        [
            BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await bets.PlaceAsync(panel: 2, amount: "10.00", ct).ConfigureAwait(false)),
        ], ct);
        var (inFlight, afterCrash) = (placed[0], placed[1]);

        // Early in the flight (≈ 3,5 s long) the leader holds its answer for 2,5 s, far beyond the 200 ms deadline.
        await flight.AtAsync(TimeSpan.FromMilliseconds(300), ct);
        cluster.LeaderProbe.HoldLeaderDecisions();
        var sentAt = DateTimeOffset.UtcNow;
        var outcome = await bets.CashOutAsync(inFlight, ct);
        var answeredAt = DateTimeOffset.UtcNow;

        // Pending because of the deadline: answered while the leader is still held (nothing decided yet), well before it frees.
        var pending = CashOutFlight.PendingOf(outcome);
        Assert.Equal(inFlight, pending.GetProperty("betId").GetGuid());
        Assert.True(answeredAt - sentAt <= TimeSpan.FromSeconds(2), $"CashOutPending answers at the 200 ms deadline, not when the leader frees ({(answeredAt - sentAt).TotalMilliseconds} ms; the leader is held 2,5 s).");
        Assert.DoesNotContain(cluster.LeaderProbe.Calls, c => c.Leading && c.RoundId.Value == flight.RoundId);
        Assert.Empty(bets.EventsOf("CashedOut", inFlight));
        var releaseAt = sentAt + TimeSpan.FromMilliseconds(2_500);
        if (releaseAt > DateTimeOffset.UtcNow)
        {
            await Task.Delay(releaseAt - DateTimeOffset.UtcNow, ct);
        }

        Assert.Empty(bets.EventsOf("CashedOut", inFlight));
        cluster.LeaderProbe.ReleaseLeader();

        // Decided later with the recorded instant of reception, not with the instant of the late decision.
        var cashedOut = await bets.WaitForEventAsync("CashedOut", inFlight, TimeSpan.FromSeconds(10));
        Assert.False(cashedOut.Payload.GetProperty("auto").GetBoolean());
        var multiplier = Json.Decimal(cashedOut.Payload, "multiplier");
        flight.AssertReceivedBetween(multiplier, sentAt, answeredAt, cashedOut.ToString());
        Assert.Equal(CashOutMath.Payout(10.00m, multiplier), Json.Decimal(cashedOut.Payload, "payout"));
        Assert.True(
            cluster.LeaderProbe.Calls.Any(c => c.Leading && c.RoundId.Value == flight.RoundId),
            $"Precondition: the leader decided the forwarded request. Leader: [{cluster.LeaderProbe.Describe()}]");

        // Received after the crash while the leader does not answer: refused, now or by CashOutRejected; never cashed out.
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        cluster.LeaderProbe.DelayNextLeaderDecision(TimeSpan.FromMilliseconds(300));
        var late = await bets.CashOutAsync(afterCrash, ct);
        if (!late.FailedWith("CASHOUT_TOO_LATE"))
        {
            _ = CashOutFlight.PendingOf(late);
            var rejected = await bets.WaitForEventAsync("CashOutRejected", afterCrash, TimeSpan.FromSeconds(10));
            Assert.Equal("CASHOUT_TOO_LATE", Json.Text(rejected.Payload, "code"));
        }

        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        Assert.Empty(bets.EventsOf("CashedOut", afterCrash));
    }

    /// <summary>Two instances of the Host on the room <c>demo</c> crashing at 2,00x, each with its observed arbiter.</summary>
    private sealed class Cluster : IAsyncDisposable
    {
        private readonly BettingHost _first;
        private readonly BettingHost _second;
        private readonly ArbiterProbe _firstProbe;
        private readonly ArbiterProbe _secondProbe;

        private Cluster(BettingHost first, ArbiterProbe firstProbe, BettingHost second, ArbiterProbe secondProbe)
        {
            _first = first;
            _second = second;
            _firstProbe = firstProbe;
            _secondProbe = secondProbe;
        }

        public BettingHost Leader => _first.IsLeading ? _first : _second;

        public BettingHost Follower => _first.IsLeading ? _second : _first;

        public ArbiterProbe LeaderProbe => _first.IsLeading ? _firstProbe : _secondProbe;

        public ArbiterProbe FollowerProbe => _first.IsLeading ? _secondProbe : _firstProbe;

        public static async Task<Cluster> StartAsync(BettingContainers containers, CancellationToken ct)
        {
            var firstProbe = new ArbiterProbe();
            var secondProbe = new ArbiterProbe();
            var first = await BettingHost.StartAsync(containers, ct, new ProgrammableWallet(), services => Configure(services, firstProbe)).ConfigureAwait(false);
            try
            {
                var second = await BettingHost.StartPeerAsync(first, services => Configure(services, secondProbe)).ConfigureAwait(false);
                await Eventually.HoldsAsync(() => first.IsLeading || second.IsLeading, TimeSpan.FromSeconds(30), () => "one instance leading the room").ConfigureAwait(false);
                return new Cluster(first, firstProbe, second, secondProbe);
            }
            catch
            {
                await first.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _second.DisposeAsync().ConfigureAwait(false);
            await _first.DisposeAsync().ConfigureAwait(false);
        }

        private static void Configure(IServiceCollection services, ArbiterProbe probe)
        {
            FixedCrashFairness.Register(services, CrashPoint);
            services.Decorate<ICashOutArbiter>((inner, provider) =>
                new ObservedCashOutArbiter(inner, provider.GetRequiredService<IRoundsLeadership>(), probe));
        }
    }
}
