using Apogeu.Betting.Contracts;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class PlaceBetTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dado o Host real (Postgres, Redis, carteira de 3.000,00), quando o jogador aposta 10,00 na fase de apostas, então a aposta fica placed na rodada aberta, com débito bet:{betId}:debit e saldo 2.990,00")]
    [Trait("Spec", "SPEC-0022:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostWhenPlayerBetsTenDuringBettingThenBetIsPlacedAndDebited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        var wallet = (FakeOperatorWallet)host.Wallet;
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);
        var clientBetId = Guid.NewGuid();

        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var placed = await bets.PlaceAsync(clientBetId, panel: 1, amount: "10.00", autoCashOut: "2.00", ct);

        var accepted = placed.Value;
        var betId = accepted.GetProperty("betId").GetGuid();
        Assert.Equal("placed", Json.Text(accepted, "status"));
        Assert.Equal(clientBetId, accepted.GetProperty("clientBetId").GetGuid());
        Assert.Equal(betting.RoundId, accepted.GetProperty("roundId").GetGuid());
        Assert.Equal(betting.Payload.GetProperty("roundNumber").GetInt64(), accepted.GetProperty("roundNumber").GetInt64());
        Assert.Equal(1, accepted.GetProperty("panel").GetInt32());
        Assert.Equal(10.00m, Json.Decimal(accepted, "amount"));
        Assert.Equal("BRL", Json.Text(accepted, "currency"));
        Assert.Equal(2.00m, Json.Decimal(accepted, "autoCashOut"));
        Assert.Equal(2990.00m, Json.Decimal(accepted, "balance"));

        var debit = Assert.Single(wallet.AppliedDebits);
        Assert.Equal($"bet:{betId}:debit", debit.TransactionId.Value);
        Assert.Equal(betId, debit.BetId);
        Assert.Equal(10.00m, debit.Amount);
        Assert.Equal(BettingHost.PlayerRef(player), debit.Player);
        Assert.Equal(2990.00m, wallet.BalanceOf(BettingHost.PlayerRef(player), BettingHost.Brl));

        // Persisted in the module's own schema (ADR-0010).
        var (tables, rows) = await host.BettingRowsAsync(ct);
        Assert.True(tables > 0 && rows > 0, $"The accepted bet is persisted in the schema betting (tables {tables}, rows {rows}).");
    }

    [Fact(DisplayName = "Dado um jogador cujo saldo na carteira não cobre a aposta, quando aposta acima do saldo, então INSUFFICIENT_FUNDS, nenhuma aposta persistida e o painel continua livre")]
    [Trait("Spec", "SPEC-0022:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenBalanceBelowAmountWhenPlayerBetsThenInsufficientFundsAndNothingPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new FakeOperatorWallet();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);

        // The demo opening balance (3.000,00) is above the maximum bet (1.000,00), so this player's wallet starts at 5,00.
        var player = "player-" + Guid.NewGuid().ToString("N");
        wallet.SetBalance(BettingHost.PlayerRef(player), BettingHost.Brl, 5.00m);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var before = await host.BettingRowsAsync(ct);
        var refused = await bets.PlaceAsync(panel: 2, amount: "10.00", ct);
        var after = await host.BettingRowsAsync(ct);

        Assert.True(refused.FailedWith(BettingErrorCodes.InsufficientFunds), $"Expected INSUFFICIENT_FUNDS, got {refused}");
        Assert.Equal(before.Rows, after.Rows);
        Assert.Empty(wallet.AppliedDebits);
        Assert.Equal(5.00m, wallet.BalanceOf(BettingHost.PlayerRef(player), BettingHost.Brl));

        // Nothing was kept for the panel: a bet the balance covers is accepted in it.
        var covered = await bets.PlaceAsync(panel: 2, amount: "5.00", ct);
        Assert.Equal(0.00m, Json.Decimal(covered.Value, "balance"));
    }
}
