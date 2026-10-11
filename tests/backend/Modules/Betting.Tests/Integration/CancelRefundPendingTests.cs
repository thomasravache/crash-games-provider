using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class CancelRefundPendingTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dada uma aposta placed e a carteira que falha no estorno, quando o jogador cancela, então BetCancelled com refundStatus pending, um segundo CancelBet devolve o mesmo BetCancelled e o estorno é confirmado depois de a carteira voltar, uma vez só")]
    [Trait("Spec", "SPEC-0022:IT-09")]
    [Trait("Category", "Integration")]
    public async Task GivenWalletFailingRefundWhenPlayerCancelsThenPendingIdempotentAndRefundedOnceLater()
    {
        var ct = TestContext.Current.CancellationToken;
        var down = 1;
        var wallet = new ProgrammableWallet { OnRollback = _ => Volatile.Read(ref down) == 1 ? new WalletUnavailable(Retryable: true) : null };
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(2));
        var betId = BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct));

        var first = (await bets.CancelAsync(betId, ct)).Value;
        Assert.Equal(betId, first.GetProperty("betId").GetGuid());
        Assert.True(first.TryGetProperty("refundStatus", out var status), $"BetCancelled has refundStatus (contract v3): {first.GetRawText()}");
        Assert.Equal("pending", status.GetString());
        Assert.Equal(10.00m, Json.Decimal(first, "refunded"));

        var second = (await bets.CancelAsync(betId, ct)).Value;
        Assert.Equal(first.GetRawText(), second.GetRawText());

        // The wallet comes back: the durable refund is confirmed, once.
        Volatile.Write(ref down, 0);
        await Eventually.HoldsAsync(
            () => wallet.Inner.AppliedRollbacks.Any(r => r.BetId == betId),
            TimeSpan.FromSeconds(60),
            () => $"the refund of bet {betId} confirmed after the wallet came back (attempts {wallet.RollbackAttempts})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        var refund = Assert.Single(wallet.Inner.AppliedRollbacks, r => r.BetId == betId);
        Assert.Equal($"bet:{betId}:rollback", refund.TransactionId.Value);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
        Assert.Equal("cancelled", await host.BetStatusAsync(betId, ct));
    }
}
