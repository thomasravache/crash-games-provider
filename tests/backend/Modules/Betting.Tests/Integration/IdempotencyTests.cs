using Apogeu.Betting.Contracts;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class IdempotencyTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dadas chamadas com o mesmo clientBetId (duas em paralelo e uma repetição), então existe um único débito e todas recebem o mesmo BetAccepted")]
    [Trait("Spec", "SPEC-0022:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenSameClientBetIdInParallelAndRepeatedThenSingleDebitAndSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        var wallet = (FakeOperatorWallet)host.Wallet;
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var a = await BetsClient.ConnectAsync(host, player, ct);
        await using var b = await BetsClient.ConnectAsync(host, player, ct);
        var clientBetId = Guid.NewGuid();

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var parallel = await Task.WhenAll(
            a.PlaceAsync(clientBetId, panel: 1, amount: "10.00", autoCashOut: null, ct),
            b.PlaceAsync(clientBetId, panel: 1, amount: "10.00", autoCashOut: null, ct));
        var repeated = await a.PlaceAsync(clientBetId, panel: 1, amount: "10.00", autoCashOut: null, ct);

        var original = parallel[0].Value;
        foreach (var outcome in (HubOutcome[])[parallel[1], repeated])
        {
            Assert.True(
                outcome.Result is { } result && string.Equals(result.GetRawText(), original.GetRawText(), StringComparison.Ordinal),
                $"Expected the original BetAccepted {original.GetRawText()}, got {outcome}");
        }

        var debit = Assert.Single(wallet.AppliedDebits);
        Assert.Equal($"bet:{original.GetProperty("betId").GetGuid()}:debit", debit.TransactionId.Value);
        Assert.Equal(2990.00m, wallet.BalanceOf(BettingHost.PlayerRef(player), BettingHost.Brl));
        Assert.Equal(2990.00m, Json.Decimal(original, "balance"));
    }

    [Fact(DisplayName = "Dada uma carteira que expira depois de debitar (e falha a primeira tentativa de estorno), quando o jogador aposta, então WALLET_UNAVAILABLE, a aposta não é aceita e o débito é estornado com nova tentativa")]
    [Trait("Spec", "SPEC-0022:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenWalletTimingOutAfterDebitWhenPlayerBetsThenWalletUnavailableAndDebitIsRolledBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new TimingOutWallet(new FakeOperatorWallet());
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var refused = await bets.PlaceAsync(panel: 1, amount: "10.00", ct);

        Assert.True(refused.FailedWith(BettingErrorCodes.WalletUnavailable), $"Expected WALLET_UNAVAILABLE, got {refused}");
        var debit = Assert.Single(wallet.Inner.AppliedDebits);
        Assert.Matches("^bet:[0-9a-f-]{36}:debit$", debit.TransactionId.Value);

        // ADR-0007: the uncertain debit is reversed automatically, retried until the wallet confirms it.
        await Eventually.HoldsAsync(
            () => wallet.Inner.AppliedRollbacks.Count == 1,
            TimeSpan.FromSeconds(30),
            () => $"the uncertain debit rolled back (attempts {wallet.RollbackAttempts}; calls: {string.Join(", ", wallet.Inner.Calls)})");
        var rollback = Assert.Single(wallet.Inner.AppliedRollbacks);
        Assert.Equal(debit.TransactionId, rollback.OriginalTransactionId);
        Assert.Equal($"bet:{debit.BetId}:rollback", rollback.TransactionId.Value);
        Assert.True(wallet.RollbackAttempts >= 2, $"The refused first rollback is retried (attempts {wallet.RollbackAttempts}).");
        Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
    }
}
