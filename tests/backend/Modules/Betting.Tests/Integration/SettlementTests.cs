using Apogeu.Betting.Contracts;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Tests.Support;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// Settlement of the rounds (SPEC-0024) on the real Host: the real conductor with the crash point fixed at 2,00x (and, when
/// armed, the reveal mismatch that annuls a round, SPEC-0008 <c>CRASH_POINT_MISMATCH</c>), real PostgreSQL and Redis, real
/// SignalR players and the wallet double of the SPEC-0021 contract as the ledger.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class SettlementTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dado o Host real com duas rodadas (uma termina em crash e outra anulada), quando liquidadas, então estados, saldos e resumos batem com o livro-razão da carteira")]
    [Trait("Spec", "SPEC-0024:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenCrashedAndAnnulledRoundsWhenSettledThenStatusesBalancesAndSummariesMatchTheLedger()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var mismatch = new RevealMismatch();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            services.Decorate<IFairnessService>((inner, _) => new AnnullingFairness(inner, mismatch));
        });
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var (name1, name2) = ("player-" + Guid.NewGuid().ToString("N"), "player-" + Guid.NewGuid().ToString("N"));
        await using var p1 = await BetsClient.ConnectAsync(host, name1, ct);
        await using var p2 = await BetsClient.ConnectAsync(host, name2, ct);

        // Round A crashes at 2,00x: p1 cashes out 10,00 and keeps 20,00 in; p2 keeps 30,00 in.
        var (flightA, a) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await p1.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await p1.PlaceAsync(panel: 2, amount: "20.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await p2.PlaceAsync(panel: 1, amount: "30.00", ct).ConfigureAwait(false)),
        ], ct);
        await flightA.AtAsync(TimeSpan.FromSeconds(1), ct);
        var payA = Json.Decimal(CashOutFlight.CashedOutOf(await p1.CashOutAsync(a[0], ct)), "payout");
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flightA.RoundId, RoundTimeout);

        // Round B is annulled at its end: p1 cashes out 10,00 during the flight; p2 keeps 30,00 in.
        mismatch.ArmNextReveal();
        var (flightB, b) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await p1.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await p2.PlaceAsync(panel: 1, amount: "30.00", ct).ConfigureAwait(false)),
        ], ct);
        await flightB.AtAsync(TimeSpan.FromSeconds(1), ct);
        var payB = Json.Decimal(CashOutFlight.CashedOutOf(await p1.CashOutAsync(b[0], ct)), "payout");
        await rounds.Recorder.WaitForAsync("RoundAnnulled", e => e.RoundId == flightB.RoundId, RoundTimeout);

        // States.
        await Settlements.WaitStatusAsync(host, a[1], "lost", ct);
        await Settlements.WaitStatusAsync(host, a[2], "lost", ct);
        await Settlements.WaitStatusAsync(host, b[1], "refunded", ct);
        Assert.Equal("cashed_out", await host.BetStatusAsync(a[0], ct));
        Assert.Equal("cashed_out", await host.BetStatusAsync(b[0], ct));

        await AssertPlayerEventsAsync(p1, p2, flightA.RoundId, a, flightB.RoundId, b);

        // Balances: the wallet's ledger.
        Assert.Equal(FakeOperatorWallet.OpeningBalance - 40.00m + payA + payB, wallet.Inner.BalanceOf(BettingHost.PlayerRef(name1), BettingHost.Brl));
        Assert.Equal(FakeOperatorWallet.OpeningBalance - 30.00m, wallet.Inner.BalanceOf(BettingHost.PlayerRef(name2), BettingHost.Brl));

        // Summaries: one per round, equal to the ledger of the round's bets.
        var summaryA = await Settlements.SingleSummaryAsync(host, flightA.RoundId, ct);
        var summaryB = await Settlements.SingleSummaryAsync(host, flightB.RoundId, ct);
        AssertMatchesLedger(summaryA, wallet, a, betsCount: 3, wagered: 60.00m, paid: payA, refundedTotal: 0.00m);
        AssertMatchesLedger(summaryB, wallet, b, betsCount: 2, wagered: 40.00m, paid: payB, refundedTotal: 30.00m);
    }

    [Fact(DisplayName = "Dado um evento de crash entregue duas vezes, quando processado, então resumo e estados não mudam na segunda vez")]
    [Trait("Spec", "SPEC-0024:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenCrashEventDeliveredTwiceWhenProcessedThenSummaryAndStatesDoNotChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            services.Decorate<IRoundFeed>((inner, _) => new RecordingRoundFeed(inner));
        });
        var feed = Assert.IsType<RecordingRoundFeed>(host.Services.GetRequiredService<IRoundFeed>());
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var player = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);

        var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await player.PlaceAsync(panel: 2, amount: "20.00", ct).ConfigureAwait(false)),
        ], ct);
        await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
        _ = CashOutFlight.CashedOutOf(await player.CashOutAsync(bets[1], ct));
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        await Settlements.WaitStatusAsync(host, bets[0], "lost", ct);
        var before = await Settlements.SingleSummaryAsync(host, flight.RoundId, ct);
        var callsBefore = wallet.Inner.Calls.Count;
        var eventsBefore = player.Events.Count;

        // The same RoundCrashed delivered again to Betting's round-events queue.
        var crashed = Assert.Single(feed.Received.OfType<RoundCrashed>(), c => c.RoundId.Value == flight.RoundId);
        var scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(crashed);
        }

        await Eventually.HoldsAsync(
            () => feed.Received.OfType<RoundCrashed>().Count(c => c.RoundId.Value == flight.RoundId) == 2,
            TimeSpan.FromSeconds(10),
            () => "the repeated RoundCrashed reaches Betting");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        Assert.Equal(before, Assert.Single(await host.SettlementsAsync(flight.RoundId, ct)));
        Assert.Equal("lost", await host.BetStatusAsync(bets[0], ct));
        Assert.Equal("cashed_out", await host.BetStatusAsync(bets[1], ct));
        Assert.Single(player.EventsOf("BetSettled", bets[0]));
        Assert.Equal(eventsBefore, player.Events.Count);
        Assert.Equal(callsBefore, wallet.Inner.Calls.Count);
    }

    [Fact(DisplayName = "Dada uma aposta active com pedido de cash-out pendente recebido antes do crash, quando a rodada crasha e o líder volta, então vira cashed_out pelo horário gravado (não lost), com um crédito e o pagamento no resumo; e um pedido gravado depois do crash termina lost")]
    [Trait("Spec", "SPEC-0024:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenPendingCashOutAtCrashWhenLeaderAnswersThenCashedOutAtRecordedInstantAndSummaryIncludesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await CashOutCluster.StartAsync(containers, crashPoint: 3.00m, new BettingSala(Cap: "3.00"), ct);
        await using var player = await BetsClient.ConnectAsync(cluster.Follower, "player-" + Guid.NewGuid().ToString("N"), ct);
        await using var rounds = await RoundWatch.StartAsync(cluster.Leader, ct);
        var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(cluster.Follower, rounds, async () =>
        [
            BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await player.PlaceAsync(panel: 2, amount: "10.00", ct).ConfigureAwait(false)),
        ], ct);
        var (before, after) = (bets[0], bets[1]);

        // Before the crash the leader stops answering: the request is recorded as pending.
        await flight.AtAsync(TimeSpan.FromSeconds(1.5), ct);
        cluster.LeaderProbe.HoldLeaderDecisions();
        var sentAt = DateTimeOffset.UtcNow;
        var pending = await player.CashOutAsync(before, ct);
        var answeredAt = DateTimeOffset.UtcNow;
        _ = CashOutFlight.PendingOf(pending);

        // The round crashes meanwhile; a request for the other bet arrives after it.
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        var late = await player.CashOutAsync(after, ct);
        Assert.True(late.FailedWith("CASHOUT_TOO_LATE") || late.Result is not null, $"Unexpected answer after the crash: {late}");

        // The bet with the request recorded before the crash is not lost while undecided, and the summary stays open.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        Assert.Equal("active", await cluster.Follower.BetStatusAsync(before, ct));
        Assert.Empty(player.EventsOf("BetSettled", before));
        Assert.Empty(await cluster.Follower.SettlementsAsync(flight.RoundId, ct));

        // The leader answers: cashed out at the recorded instant, credited once; the late one is lost.
        cluster.LeaderProbe.ReleaseLeader();
        var cashedOut = await player.WaitForEventAsync("CashedOut", before, TimeSpan.FromSeconds(30));
        var multiplier = Json.Decimal(cashedOut.Payload, "multiplier");
        flight.AssertReceivedBetween(multiplier, sentAt, answeredAt, cashedOut.ToString());
        await Settlements.WaitStatusAsync(cluster.Follower, before, "cashed_out", ct);
        await Settlements.WaitStatusAsync(cluster.Follower, after, "lost", ct);
        Assert.Equal("lost", Json.Text((await player.WaitForEventAsync("BetSettled", after, TimeSpan.FromSeconds(10))).Payload, "status"));
        var payout = CashOutMath.Payout(10.00m, multiplier);
        var credit = Assert.Single(cluster.Wallet.Inner.AppliedCredits, c => c.BetId == before);
        Assert.Equal(payout, credit.Amount);
        Assert.Empty(player.EventsOf("BetSettled", before));

        var summary = await Settlements.SingleSummaryAsync(cluster.Follower, flight.RoundId, ct);
        Assert.Equal(2, summary.BetsCount);
        Assert.Equal(20.00m, summary.TotalWagered.Value);
        Assert.Equal(payout, summary.TotalPaid.Value);
        Assert.Equal(0.00m, summary.TotalRefunded.Value);
    }

    /// <summary>IT-01: BetSettled for the lost bets of round A, BetRefunded for the refunded bet of round B, nothing for the cashed out.</summary>
    private static async Task AssertPlayerEventsAsync(BetsClient p1, BetsClient p2, Guid roundA, IReadOnlyList<Guid> a, Guid roundB, IReadOnlyList<Guid> b)
    {
        var lostA1 = await p1.WaitForEventAsync("BetSettled", a[1], TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        var lostA2 = await p2.WaitForEventAsync("BetSettled", a[2], TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        foreach (var settled in (HubEvent[])[lostA1, lostA2])
        {
            Assert.Equal("lost", Json.Text(settled.Payload, "status"));
            Assert.Equal(roundA, settled.Payload.GetProperty("roundId").GetGuid());
        }

        var refunded = (await p2.WaitForEventAsync("BetRefunded", b[1], TimeSpan.FromSeconds(10)).ConfigureAwait(false)).Payload;
        Assert.Equal(roundB, refunded.GetProperty("roundId").GetGuid());
        Assert.Equal(30.00m, Json.Decimal(refunded, "refunded"));
        Assert.Equal("refunded", Json.Text(refunded, "refundStatus"));
        Assert.Equal(FakeOperatorWallet.OpeningBalance - 30.00m, Json.Decimal(refunded, "balance"));
        Assert.Empty(p1.EventsOf("BetSettled", a[0]));
        Assert.Empty(p1.EventsOf("BetRefunded", b[0]));
    }

    /// <summary>The summary of a round equals its figures and the wallet's ledger of the round's bets (debits, credits, reversals).</summary>
    private static void AssertMatchesLedger(RoundSettlement summary, ProgrammableWallet wallet, IReadOnlyList<Guid> bets, int betsCount, decimal wagered, decimal paid, decimal refundedTotal)
    {
        Assert.Equal(betsCount, summary.BetsCount);
        Assert.Equal(wagered, summary.TotalWagered.Value);
        Assert.Equal(paid, summary.TotalPaid.Value);
        Assert.Equal(refundedTotal, summary.TotalRefunded.Value);
        Assert.Equal(wagered - paid - refundedTotal, summary.Ggr.Value);

        var debits = wallet.Inner.AppliedDebits.Where(d => bets.Contains(d.BetId)).ToList();
        Assert.Equal(summary.TotalWagered.Value, debits.Sum(d => d.Amount!.Value));
        Assert.Equal(summary.TotalPaid.Value, wallet.Inner.AppliedCredits.Where(c => bets.Contains(c.BetId)).Sum(c => c.Amount!.Value));
        var reversed = wallet.Inner.AppliedRollbacks.Where(r => bets.Contains(r.BetId)).Select(r => debits.Single(d => d.TransactionId == r.OriginalTransactionId).Amount!.Value);
        Assert.Equal(summary.TotalRefunded.Value, reversed.Sum());
    }
}
