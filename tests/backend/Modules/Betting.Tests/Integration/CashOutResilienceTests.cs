using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Features.CashOut;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// The cash-out across failures (SPEC-0023 review G4): a leader that fails over or does not answer, an automatic cash-out whose
/// publication fails, and concurrent pending requests — on real Hosts, PostgreSQL and Redis, with the crash point fixed.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class CashOutResilienceTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dada uma aposta com alvo 1,50x numa rodada com crash 5,00x e o líder interrompido em 1,2x (outra instância assume), quando a rodada termina, então a aposta é retirada em 1,50x exatos com CashedOut auto e um único crédito")]
    [Trait("Spec", "SPEC-0023:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenAutoBelowCrashWhenLeaderFailsOverMidFlightThenCashedOutAtTargetOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await CashOutCluster.StartAsync(containers, crashPoint: 5.00m, new BettingSala(Cap: "5.00"), ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var bets = await BetsClient.ConnectAsync(cluster.Follower, player, ct);
        CashOutFlight flight;
        Guid betId;
        await using (var rounds = await RoundWatch.StartAsync(cluster.Leader, ct))
        {
            (flight, var placed) = await CashOutFlight.PlaceAndFlyAsync(
                cluster.Follower,
                rounds,
                async () => [BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 1, amount: "10.00", autoCashOut: "1.50", ct).ConfigureAwait(false))],
                ct);
            betId = placed[0];

        }

        // The leader stops at 1,2x (≈ 0,91 s of flight), before t_alvo of 1,50x (≈ 2,03 s): the follower takes the room over.
        await flight.AtAsync(CashOutMath.TimeTo(flight.K, 1.20m), ct);
        await cluster.Leader.KillAsync(TimeSpan.FromSeconds(10));

        await AssertAutoCashedOutOnceAsync(bets, cluster.Wallet, betId, ct);
    }

    [Fact(DisplayName = "Dada uma aposta com alvo 1,50x e crash 2,00x, quando a publicação do automático falha uma vez, então a aposta é retirada em 1,50x exatos com CashedOut auto e um único crédito")]
    [Trait("Spec", "SPEC-0023:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenAutoBelowCrashWhenItsPublicationFailsOnceThenCashedOutAtTargetOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        FailingAutoCashOutSink? sink = null;
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            services.Decorate<IAutoCashOutSink>((inner, _) => sink = new FailingAutoCashOutSink(inner));
        });
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var bets = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);

        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(
            host,
            rounds,
            async () => [BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 1, amount: "10.00", autoCashOut: "1.50", ct).ConfigureAwait(false))],
            ct);
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        Assert.True(sink is { Failures: 1 }, "Precondition: the publication of the automatic cash-out failed once.");

        await AssertAutoCashedOutOnceAsync(bets, wallet, placed[0], ct);
    }

    [Fact(DisplayName = "Dado um cash-out pendente recebido em 1,8x (crash 3,00x) e o líder interrompido antes de decidir, quando outra instância assume, então a decisão é Accepted com o multiplicador do horário gravado (não TooLate)")]
    [Trait("Spec", "SPEC-0023:IT-09")]
    [Trait("Category", "Integration")]
    public async Task GivenPendingCashOutWhenLeaderDiesBeforeDecidingThenNewLeaderAcceptsAtRecordedInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await CashOutCluster.StartAsync(containers, crashPoint: 3.00m, new BettingSala(Cap: "3.00"), ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var bets = await BetsClient.ConnectAsync(cluster.Follower, player, ct);
        CashOutFlight flight;
        Guid betId;
        await using (var rounds = await RoundWatch.StartAsync(cluster.Leader, ct))
        {
            (flight, var placed) = await CashOutFlight.PlaceAndFlyAsync(
                cluster.Follower,
                rounds,
                async () => [BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false))],
                ct);
            betId = placed[0];
        }

        // At 1,8x the leader does not answer: the follower records the request as pending.
        await flight.AtAsync(CashOutMath.TimeTo(flight.K, 1.80m), ct);
        cluster.LeaderProbe.HoldLeaderDecisions();
        var sentAt = DateTimeOffset.UtcNow;
        var outcome = await bets.CashOutAsync(betId, ct);
        var answeredAt = DateTimeOffset.UtcNow;
        _ = CashOutFlight.PendingOf(outcome);

        // The leader dies without deciding; the follower takes over, its conductor slow to resume the flight.
        cluster.FollowerGate.CloseFor(TimeSpan.FromSeconds(15));
        await cluster.Leader.KillAsync(TimeSpan.FromSeconds(10));

        var decided = await WaitForDecisionAsync(bets, betId);
        Assert.True(
            string.Equals(decided.Target, "CashedOut", StringComparison.Ordinal),
            $"The pending request received at 1,8x, before the crash at 3,00x, is accepted by the new leader; got {decided}. Follower: [{cluster.FollowerProbe.Describe()}]");
        Assert.False(decided.Payload.GetProperty("auto").GetBoolean());
        flight.AssertReceivedBetween(Json.Decimal(decided.Payload, "multiplier"), sentAt, answeredAt, decided.ToString());
    }

    [Fact(DisplayName = "Dados 300 automáticos em 1,50x com a carteira indisponível, quando a rodada crasha e a seguinte abre, então RoundCrashed é processado e as apostas da rodada seguinte são ativadas em ≤ 2 s, e os créditos são confirmados quando a carteira volta")]
    [Trait("Spec", "SPEC-0023:IT-10")]
    [Trait("Category", "Integration")]
    public async Task GivenThreeHundredAutosWithWalletDownWhenRoundCrashesThenRoundEventsAreNotDelayedAndCreditsLaterConfirmed()
    {
        const int players = 150;
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var walletDown = 1;
        wallet.OnCredit = _ => Volatile.Read(ref walletDown) == 1 ? new WalletUnavailable(Retryable: true) : null;
        var sala = new BettingSala(BettingDuration: "00:00:10");
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services => FixedCrashFairness.Register(services, 2.00m), sala);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var clients = new List<BetsClient>(players);
        try
        {
            for (var player = 0; player < players; player++)
            {
                clients.Add(await BetsClient.ConnectAsync(host, $"auto-{player}-{Guid.NewGuid():N}", ct));
            }

            await using var late = await BetsClient.ConnectAsync(host, "late-" + Guid.NewGuid().ToString("N"), ct);
            var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, () => PlaceAutosAsync(clients, ct), ct, bettingLeft: TimeSpan.FromSeconds(8));
            Assert.Equal(players * 2, bets.Count);

            // A bet for the next round, made during the flight: activated when the next round opens.
            var queued = BetsClient.BetId(await late.PlaceAsync(panel: 1, amount: "10.00", ct));
            var crashed = await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
            var crashSeen = await Eventually.HoldsAsync(
                () => FeedEnded(host, flight.RoundId),
                TimeSpan.FromSeconds(30),
                () => $"Betting processes RoundCrashed of {flight.RoundId}");
            Assert.True(
                DateTimeOffset.UtcNow - crashed.ReceivedAt <= TimeSpan.FromSeconds(2),
                $"RoundCrashed was processed by Betting {(DateTimeOffset.UtcNow - crashed.ReceivedAt).TotalSeconds:0.0} s after the crash (budget 2 s; waited {crashSeen.TotalSeconds:0.0} s).");

            var next = await rounds.Recorder.WaitForAsync("RoundBetting", e => e.RoundId != flight.RoundId && e.ReceivedAt > crashed.ReceivedAt, RoundTimeout);
            var activated = await late.WaitForActivationAsync(queued, TimeSpan.FromSeconds(30));
            Assert.Equal(next.RoundId, activated.GetProperty("roundId").GetGuid());
            var activationDelay = DateTimeOffset.UtcNow - next.ReceivedAt;
            Assert.True(activationDelay <= TimeSpan.FromSeconds(2), $"The next round's bet was activated {activationDelay.TotalSeconds:0.0} s after its bets opened (budget 2 s).");

            // The wallet comes back: every credit of the 300 automatic cash-outs is confirmed, once.
            Volatile.Write(ref walletDown, 0);
            await Eventually.HoldsAsync(
                () => wallet.Inner.AppliedCredits.Count == bets.Count,
                TimeSpan.FromSeconds(90),
                () => $"{wallet.Inner.AppliedCredits.Count} of {bets.Count} credits confirmed");
            Assert.All(wallet.Inner.AppliedCredits, credit => Assert.Equal(1.50m, credit.Amount));
            Assert.Equal(bets.Count, wallet.Inner.AppliedCredits.Select(c => c.BetId).Distinct().Count());
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    [Fact(DisplayName = "Dados dois CashOut concorrentes para a mesma aposta com o líder sem responder, quando gravados como pendentes, então o horário gravado é o do primeiro recebido e existe uma única decisão")]
    [Trait("Spec", "SPEC-0023:IT-11")]
    [Trait("Category", "Integration")]
    public async Task GivenConcurrentPendingCashOutsWhenRecordedThenFirstReceptionStaysAndOneDecision()
    {
        const int connections = 6;
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await CashOutCluster.StartAsync(containers, crashPoint: 3.00m, new BettingSala(Cap: "3.00"), ct);
        var player = "player-" + Guid.NewGuid().ToString("N");

        // The same player on several connections, so the requests run in parallel on the follower.
        var clients = new List<BetsClient>(connections);
        try
        {
            for (var i = 0; i < connections; i++)
            {
                clients.Add(await BetsClient.ConnectAsync(cluster.Follower, player, ct));
            }

            await using var rounds = await RoundWatch.StartAsync(cluster.Leader, ct);
            var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(
                cluster.Follower,
                rounds,
                async () => [BetsClient.BetId(await clients[0].PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false))],
                ct);
            var betId = placed[0];

            await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
            cluster.LeaderProbe.HoldLeaderDecisions();
            var outcomes = await Task.WhenAll(clients.Select(client => client.CashOutAsync(betId, ct)));
            Assert.All(outcomes, outcome => CashOutFlight.PendingOf(outcome));

            // The recorded instant is the first reception among the requests the follower received.
            var received = cluster.FollowerProbe.Requests.Where(r => !r.Leading && r.RoundId.Value == flight.RoundId).Select(r => r.ReceivedAt).ToList();
            Assert.True(received.Count >= connections, $"Precondition: the follower asked the leader for every request ({received.Count}).");
            var first = received.Min();

            // PostgreSQL keeps microseconds.
            var firstInMicroseconds = new DateTimeOffset(first.UtcTicks - (first.UtcTicks % 10), TimeSpan.Zero);
            Assert.Equal(firstInMicroseconds, await cluster.Follower.CashOutRequestedAtAsync(betId, ct));

            // One decision, for that instant: one CashedOut per connection of the player, one credit.
            cluster.LeaderProbe.ReleaseLeader();
            var cashedOut = await clients[0].WaitForEventAsync("CashedOut", betId, TimeSpan.FromSeconds(30));
            await Task.Delay(TimeSpan.FromSeconds(4), ct);
            Assert.Single(clients[0].EventsOf("CashedOut", betId));
            Assert.Empty(clients[0].EventsOf("CashOutRejected", betId));
            flight.AssertReceivedBetween(Json.Decimal(cashedOut.Payload, "multiplier"), first, first, cashedOut.ToString());
            Assert.Single(cluster.Wallet.Inner.AppliedCredits, c => c.BetId == betId);
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    /// <summary>The automatic cash-out of <paramref name="betId"/> at 1,50x exactly, notified once and credited once (15,00).</summary>
    private static async Task AssertAutoCashedOutOnceAsync(BetsClient bets, ProgrammableWallet wallet, Guid betId, CancellationToken ct)
    {
        var cashedOut = await bets.WaitForEventAsync("CashedOut", betId, RoundTimeout).ConfigureAwait(false);
        Assert.True(cashedOut.Payload.GetProperty("auto").GetBoolean(), $"The automatic cash-out is auto:true: {cashedOut}");
        Assert.Equal("1.50", Json.Text(cashedOut.Payload, "multiplier"));
        Assert.Equal(15.00m, Json.Decimal(cashedOut.Payload, "payout"));

        await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
        Assert.Single(bets.EventsOf("CashedOut", betId));
        var credit = Assert.Single(wallet.Inner.AppliedCredits, c => c.BetId == betId);
        Assert.Equal(TransactionId.ForCredit(betId), credit.TransactionId);
        Assert.Equal(15.00m, credit.Amount);
    }

    /// <summary>The late decision of a pending cash-out: its <c>CashedOut</c> or its <c>CashOutRejected</c>.</summary>
    private static async Task<HubEvent> WaitForDecisionAsync(BetsClient bets, Guid betId)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < RoundTimeout)
        {
            if (bets.EventsOf("CashedOut", betId).Concat(bets.EventsOf("CashOutRejected", betId)).FirstOrDefault() is { } decided)
            {
                return decided;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        Assert.Fail($"No decision for the pending cash-out of {betId} within {RoundTimeout}. Received:{Environment.NewLine}{bets.DescribeEvents()}");
        return null!;
    }

    private static bool FeedEnded(BettingHost host, Guid roundId) =>
        host.Services.GetRequiredService<Apogeu.Betting.Features.RoundEvents.IRoundFeed>().Current(BettingSala.SalaId) is { } current
        && (current.RoundId.Value != roundId || current.Phase == Apogeu.Betting.Domain.RoundPhase.Ended);

    /// <summary>Each player bets 1,00 with an automatic cash-out at 1,50x in both panels, 20 bets at a time.</summary>
    private static async Task<IReadOnlyList<Guid>> PlaceAutosAsync(List<BetsClient> clients, CancellationToken ct)
    {
        var placing = clients.SelectMany(client => (int[])[1, 2], (client, panel) => (client, panel)).ToList();
        var ids = new List<Guid>(placing.Count);
        foreach (var batch in placing.Chunk(20))
        {
            var outcomes = await Task.WhenAll(batch.Select(b => b.client.PlaceAsync(Guid.NewGuid(), b.panel, "1.00", "1.50", ct))).ConfigureAwait(false);
            ids.AddRange(outcomes.Select(BetsClient.BetId));
        }

        return ids;
    }
}
