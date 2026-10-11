using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-07: two different debits that together exceed the balance, at the same time for the same player (double bet), on
/// real PostgreSQL: one applies and the other is refused; the balance never goes negative (SPEC-0021 §5).
/// </summary>
[Collection(WalletPostgres.Name)]
public sealed class ConcurrentDistinctDebitsTests(PostgreSqlContainerFixture postgres)
{
    private const int Repetitions = 20;

    [Fact(DisplayName = "Dado saldo 15,00 e PostgreSQL real, quando dois débitos diferentes de 10,00 chegam ao mesmo tempo (20 repetições), então um recebe Ok e o outro InsufficientFunds e o saldo nunca fica negativo")]
    [Trait("Spec", "SPEC-0021:IT-07")]
    [Trait("Category", "Integration")]
    public async Task GivenFifteenBalanceWhenTwoDistinctDebitsOfTenRaceThenOneOkAndOneInsufficientFunds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);

        for (var repetition = 0; repetition < Repetitions; repetition++)
        {
            var player = NewPlayer();
            AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 2_985.00m, Guid.NewGuid()), ct)), 15.00m);
            var first = Debit(player, 10.00m, Guid.NewGuid());
            var second = Debit(player, 10.00m, Guid.NewGuid());
            using var start = new ManualResetEventSlim(initialState: false);

            var calls = new[] { first, second }
                .Select(debit => Task.Run(
                    async () =>
                    {
                        start.Wait(ct);
                        return await module.WithWalletAsync(wallet => wallet.DebitAsync(debit, ct)).ConfigureAwait(false);
                    },
                    ct))
                .ToArray();
            start.Set();
            var results = await Task.WhenAll(calls);

            var ok = Assert.Single(results.OfType<WalletOk>());
            Assert.Equal(BrlBalance(5.00m), ok.BalanceAfter);
            var refused = Assert.Single(results.OfType<WalletInsufficientFunds>());
            Assert.True(refused.Balance.Amount.Value >= 0m, $"Repetition {repetition}: the refused debit saw a negative balance {refused.Balance.Amount.Value}.");

            var balance = await module.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct));
            Assert.Equal(BrlBalance(5.00m), balance);
            var applied = await LedgerRows.CountEntriesAsync(module.ConnectionString, first.TransactionId, ct)
                + await LedgerRows.CountEntriesAsync(module.ConnectionString, second.TransactionId, ct);
            Assert.Equal(1, applied);
        }
    }
}
