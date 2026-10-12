using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// Settlement against the races of review G4 (SPEC-0024): a cash-out accepted before the crash but committed after Betting
/// settled the crash, and a placed bet whose activation was lost — on the real Host with faults of its <c>IBetStore</c>
/// (<see cref="BetStoreFaults"/>) and every round crashing at 2,00x.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class SettlementRaceTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dado um cash-out recebido pouco antes do crash, o líder aceitando e a Betting processando o RoundCrashed antes de o hub gravar o cash-out, quando tudo termina, então a aposta é cashed_out pelo horário de recebimento, com um único crédito, o resumo inclui o pagamento e o jogador recebe CashedOut")]
    [Trait("Spec", "SPEC-0024:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenCashOutAcceptedBeforeCrashWhenCrashIsSettledBeforeItsCommitThenCashedOutOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var plan = new BetStoreFaults.Plan();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            BetStoreFaults.Register(services, plan);
        });
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var player = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);
        await using var other = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);
        var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await other.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
        ], ct);
        var (cashing, sentinel) = (bets[0], bets[1]);

        // The commit of the cash-out waits until Betting settled the crash (the other bet, without a cash-out, is lost).
        plan.HoldCashOut(cashing, () => UntilLostAsync(host, sentinel));
        var crashAt = flight.StartedAt + CashOutMath.TimeTo(flight.K, 2.00m);
        await flight.AtAsync(CashOutMath.TimeTo(flight.K, 1.90m), ct);
        var sentAt = DateTimeOffset.UtcNow;
        var outcome = await player.CashOutAsync(cashing, ct);
        Assert.True(sentAt < crashAt, $"Precondition: the cash-out was sent before the crash ({sentAt:O} < {crashAt:O}).");
        Assert.Equal("lost", await host.BetStatusAsync(sentinel, ct));

        var cashedOut = CashOutFlight.CashedOutOf(outcome);
        var multiplier = Json.Decimal(cashedOut, "multiplier");
        flight.AssertReceivedBetween(multiplier, sentAt, crashAt - TimeSpan.FromMilliseconds(2), outcome.ToString());
        var payout = CashOutMath.Payout(10.00m, multiplier);
        Assert.Equal(payout, Json.Decimal(cashedOut, "payout"));

        await Settlements.WaitStatusAsync(host, cashing, "cashed_out", ct);
        var credit = Assert.Single(wallet.Inner.AppliedCredits, c => c.BetId == cashing);
        Assert.Equal(payout, credit.Amount);
        Assert.Empty(player.EventsOf("BetSettled", cashing));
        var summary = await Settlements.SingleSummaryAsync(host, flight.RoundId, ct);
        Assert.Equal(2, summary.BetsCount);
        Assert.Equal(20.00m, summary.TotalWagered.Value);
        Assert.Equal(payout, summary.TotalPaid.Value);
    }

    [Fact(DisplayName = "Dada uma aposta placed cuja ativação foi perdida numa rodada que crasha, quando a liquidação roda, então a aposta é estornada integralmente com BetRefunded, há alerta de conciliação e o resumo fecha")]
    [Trait("Spec", "SPEC-0024:IT-07")]
    [Trait("Category", "Integration")]
    public async Task GivenPlacedBetWithLostActivationWhenCrashIsSettledThenRefundedWithAlertAndSummaryClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var plan = new BetStoreFaults.Plan();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            BetStoreFaults.Register(services, plan);
        });
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var name = "player-" + Guid.NewGuid().ToString("N");
        await using var player = await BetsClient.ConnectAsync(host, name, ct);

        // The flight of the bet's round does not activate it (IBetStore.StartFlightAsync does nothing).
        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var betId = BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct));
        plan.ActivationLost = true;
        var flying = await rounds.NextFlightAsync();
        Assert.Equal(betting.RoundId, flying.RoundId);
        await Eventually.HoldsAsync(() => plan.LostActivations >= 1, TimeSpan.FromSeconds(10), () => "the activation of the flight lost");
        plan.ActivationLost = false;
        Assert.Equal("placed", await host.BetStatusAsync(betId, ct));

        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flying.RoundId, RoundTimeout);

        // Refunded in full, told to the player, with a reconciliation alert naming the bet.
        await Settlements.WaitStatusAsync(host, betId, "refunded", ct);
        var refunded = (await player.WaitForEventAsync("BetRefunded", betId, TimeSpan.FromSeconds(30))).Payload;
        Assert.Equal(10.00m, Json.Decimal(refunded, "refunded"));
        Assert.Equal(flying.RoundId, refunded.GetProperty("roundId").GetGuid());
        await Eventually.HoldsAsync(
            () => wallet.Inner.AppliedRollbacks.Any(r => r.BetId == betId),
            TimeSpan.FromSeconds(30),
            () => $"the debit of bet {betId} reversed");
        var reversal = Assert.Single(wallet.Inner.AppliedRollbacks, r => r.BetId == betId);
        Assert.Equal(TransactionId.ForDebit(betId), reversal.OriginalTransactionId);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(BettingHost.PlayerRef(name), BettingHost.Brl));
        var id = betId.ToString();
        Assert.True(
            host.Logs.Entries.Any(e => e.Level >= LogLevel.Warning && e.Message.Contains(id, StringComparison.OrdinalIgnoreCase)),
            $"Expected a reconciliation alert (log ≥ Warning naming bet {id}).");

        var summary = await Settlements.SingleSummaryAsync(host, flying.RoundId, ct);
        Assert.Equal(1, summary.BetsCount);
        Assert.Equal(10.00m, summary.TotalRefunded.Value);
        Assert.Equal(0.00m, summary.Ggr.Value);
    }

    /// <summary>Until <paramref name="betId"/> is lost (the crash was settled), at most 15 s.</summary>
    private static async Task UntilLostAsync(BettingHost host, Guid betId)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15)
            && !string.Equals(await host.BetStatusAsync(betId, CancellationToken.None).ConfigureAwait(false), "lost", StringComparison.Ordinal))
        {
            await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
