using Apogeu.Betting.Tests.Support;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class SettlementRecoveryTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dada uma carteira que falha no estorno e um reinício do processo, quando a varredura roda, então BetRefunded chega com refundStatus pending e balance nulo, o estorno é confirmado uma única vez e BalanceUpdated chega ao jogador")]
    [Trait("Spec", "SPEC-0024:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenFailingRefundAndRestartWhenSweepRunsThenPendingRefundIsConfirmedOnceWithBalanceUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var mismatch = new RevealMismatch();
        var name = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(name);
        var refundsDown = 1;
        wallet.OnRollback = op => op.Player == account && Volatile.Read(ref refundsDown) == 1 ? new WalletUnavailable(Retryable: true) : null;

        var first = await BettingHost.StartAsync(containers, ct, wallet, services => Annulling(services, mismatch));
        try
        {
            await using var second = await BettingHost.StartPeerAsync(first, services => Annulling(services, mismatch));
            await Eventually.HoldsAsync(() => first.IsLeading || second.IsLeading, TimeSpan.FromSeconds(30), () => "one instance leading the room");
            var (leader, survivor) = first.IsLeading ? (first, second) : (second, first);

            // The player stays on the instance that survives; the leader settles the annulled round.
            await using var player = await BetsClient.ConnectAsync(survivor, name, ct);
            CashOutFlight flight;
            Guid betId;
            await using (var rounds = await RoundWatch.StartAsync(leader, ct))
            {
                (flight, var placed) = await CashOutFlight.PlaceAndFlyAsync(
                    survivor,
                    rounds,
                    async () => [BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false))],
                    ct);
                betId = placed[0];
                mismatch.ArmNextReveal();
                await rounds.Recorder.WaitForAsync("RoundAnnulled", e => e.RoundId == flight.RoundId, TimeSpan.FromSeconds(60));
            }

            // The wallet is down: the refund is pending, without a balance.
            var pending = (await player.WaitForEventAsync("BetRefunded", betId, TimeSpan.FromSeconds(30))).Payload;
            Assert.Equal("pending", Json.Text(pending, "refundStatus"));
            Assert.True(Json.IsNullOrMissing(pending, "balance"), $"No balance while the refund is pending: {pending.GetRawText()}");
            Assert.Equal(10.00m, Json.Decimal(pending, "refunded"));
            Assert.True(wallet.RollbackAttempts >= 1, "Precondition: the refund was attempted and failed.");
            Assert.DoesNotContain(wallet.Inner.AppliedRollbacks, r => r.BetId == betId);

            // The process that settled the round stops for good; the wallet comes back.
            await leader.KillAsync(TimeSpan.FromSeconds(10));
            Volatile.Write(ref refundsDown, 0);

            await AssertRefundConfirmedOnceAsync(survivor, player, wallet, account, betId, flight.RoundId, ct);
        }
        finally
        {
            await first.DisposeAsync();
        }
    }

    /// <summary>The refund is confirmed once after the restart: BalanceUpdated, one reversal of the debit, refunded, summary.</summary>
    private static async Task AssertRefundConfirmedOnceAsync(BettingHost survivor, BetsClient player, ProgrammableWallet wallet, PlayerRef account, Guid betId, Guid roundId, CancellationToken ct)
    {
        var updated = await player.WaitForEventAsync("BalanceUpdated", betId: null, TimeSpan.FromSeconds(90)).ConfigureAwait(false);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, Json.Decimal(updated.Payload, "balance"));
        await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
        var reversal = Assert.Single(wallet.Inner.AppliedRollbacks, r => r.BetId == betId);
        Assert.Equal(TransactionId.ForDebit(betId), reversal.OriginalTransactionId);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
        Assert.Equal("refunded", await survivor.BetStatusAsync(betId, ct).ConfigureAwait(false));

        var summary = await Settlements.SingleSummaryAsync(survivor, roundId, ct).ConfigureAwait(false);
        Assert.Equal(1, summary.BetsCount);
        Assert.Equal(10.00m, summary.TotalRefunded.Value);
        Assert.Equal(0.00m, summary.Ggr.Value);
    }

    private static void Annulling(Microsoft.Extensions.DependencyInjection.IServiceCollection services, RevealMismatch mismatch)
    {
        FixedCrashFairness.Register(services, 2.00m);
        services.Decorate<IFairnessService>((inner, _) => new AnnullingFairness(inner, mismatch));
    }
}
