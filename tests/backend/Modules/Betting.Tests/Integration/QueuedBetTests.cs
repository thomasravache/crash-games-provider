using Apogeu.Betting.Contracts;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class QueuedBetTests(BettingContainers containers)
{
    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Dada uma rodada voando, quando o jogador aposta, recebe BetActivated na rodada seguinte, aposta de novo e cancela na fase de apostas, então os estados e o saldo final batem com estornos integrais")]
    [Trait("Spec", "SPEC-0022:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenFlyingRoundWhenPlayerBetsIsActivatedBetsAgainAndCancelsThenStatesAndBalanceMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        var wallet = (FakeOperatorWallet)host.Wallet;
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        // 1. During a flight: queued for the next round, debited now.
        await rounds.NextFlightAsync();
        var queued = (await bets.PlaceAsync(panel: 1, amount: "10.00", ct)).Value;
        var first = queued.GetProperty("betId").GetGuid();
        Assert.Equal("queued", Json.Text(queued, "status"));
        Assert.True(Json.IsNullOrMissing(queued, "roundId"), $"A queued bet has no round yet: {queued.GetRawText()}");
        Assert.True(Json.IsNullOrMissing(queued, "roundNumber"), $"A queued bet has no round number yet: {queued.GetRawText()}");
        Assert.Equal(2990.00m, Json.Decimal(queued, "balance"));

        // 2. The next round opens its bets: only this connection gets BetActivated with that round.
        var activated = await bets.WaitForActivationAsync(first, ActivationTimeout);
        var next = activated.GetProperty("roundId").GetGuid();
        var nextBetting = rounds.BettingOf(next);
        Assert.Equal(nextBetting.Payload.GetProperty("roundNumber").GetInt64(), activated.GetProperty("roundNumber").GetInt64());

        // 3. Bets again in the betting phase of that round and cancels it: full refund.
        var placed = (await bets.PlaceAsync(panel: 2, amount: "20.00", ct)).Value;
        var second = placed.GetProperty("betId").GetGuid();
        Assert.Equal("placed", Json.Text(placed, "status"));
        Assert.Equal(next, placed.GetProperty("roundId").GetGuid());
        Assert.Equal(2970.00m, Json.Decimal(placed, "balance"));

        var cancelled = (await bets.CancelAsync(second, ct)).Value;
        Assert.Equal(second, cancelled.GetProperty("betId").GetGuid());
        Assert.Equal(20.00m, Json.Decimal(cancelled, "refunded"));
        Assert.Equal(2990.00m, Json.Decimal(cancelled, "balance"));

        // 4. The flight of that round starts: a new bet is queued again and can be cancelled; the activated one cannot.
        var flight = await rounds.NextFlightAsync();
        Assert.Equal(next, flight.RoundId);
        var third = BetsClient.BetId(await bets.PlaceAsync(panel: 2, amount: "5.00", ct));
        var thirdCancelled = (await bets.CancelAsync(third, ct)).Value;
        Assert.Equal(5.00m, Json.Decimal(thirdCancelled, "refunded"));
        Assert.Equal(2990.00m, Json.Decimal(thirdCancelled, "balance"));

        var refused = await bets.CancelAsync(first, ct);
        Assert.True(refused.FailedWith(BettingErrorCodes.CancelNotAllowed), $"The active bet cannot be cancelled, got {refused}");

        // The wallet saw three debits and the two full refunds of the cancelled bets, nothing else.
        Assert.Equal(3, wallet.AppliedDebits.Count);
        var refunds = wallet.AppliedRollbacks;
        Assert.Equal(2, refunds.Count);
        Assert.Contains(refunds, r => r.BetId == second && string.Equals(r.OriginalTransactionId?.Value, $"bet:{second}:debit", StringComparison.Ordinal) && string.Equals(r.TransactionId.Value, $"bet:{second}:rollback", StringComparison.Ordinal));
        Assert.Contains(refunds, r => r.BetId == third && string.Equals(r.OriginalTransactionId?.Value, $"bet:{third}:debit", StringComparison.Ordinal) && string.Equals(r.TransactionId.Value, $"bet:{third}:rollback", StringComparison.Ordinal));
        Assert.Equal(2990.00m, wallet.BalanceOf(BettingHost.PlayerRef(player), BettingHost.Brl));
    }
}
