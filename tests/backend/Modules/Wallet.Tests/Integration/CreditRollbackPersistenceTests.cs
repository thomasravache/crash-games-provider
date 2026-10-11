using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>IT-03: debit, credit and rollback on real PostgreSQL, repeated, without duplicated entries (SPEC-0021 §5).</summary>
[Collection(WalletPostgres.Name)]
public sealed class CreditRollbackPersistenceTests(PostgreSqlContainerFixture postgres)
{
    [Fact(DisplayName = "Dado débito, crédito e estorno reais, quando repetidos, então o saldo final é 3.025,00 e não há lançamento duplicado; estorno sem débito → TransactionNotFound sem lançamento")]
    [Trait("Spec", "SPEC-0021:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenRealDebitCreditAndRollbackWhenRepeatedThenFinalBalanceAndNoDuplicatedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);
        var player = NewPlayer();
        var bet = Guid.NewGuid();
        var orphanBet = Guid.NewGuid();

        AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct)), 2_990.00m);
        AssertOk(await module.WithWalletAsync(wallet => wallet.CreditAsync(Credit(player, 25.00m, bet), ct)), 3_015.00m);
        AssertOk(await module.WithWalletAsync(wallet => wallet.RollbackAsync(Rollback(player, bet), ct)), 3_025.00m);

        // The same three operations again, from another "process" of the module.
        await using (var again = await WalletServices.OpenAsync(module.ConnectionString, ct))
        {
            AssertDuplicateOfOk(await again.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct)), 2_990.00m);
            AssertDuplicateOfOk(await again.WithWalletAsync(wallet => wallet.CreditAsync(Credit(player, 25.00m, bet), ct)), 3_015.00m);
            AssertDuplicateOfOk(await again.WithWalletAsync(wallet => wallet.RollbackAsync(Rollback(player, bet), ct)), 3_025.00m);

            var orphan = await again.WithWalletAsync(wallet => wallet.RollbackAsync(Rollback(player, orphanBet), ct));
            var notFound = Assert.IsType<WalletTransactionNotFound>(orphan);
            Assert.Equal(DebitId(orphanBet), notFound.OriginalTransactionId);

            Assert.Equal(BrlBalance(3_025.00m), await again.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct)));
        }

        Assert.Equal(3, await LedgerRows.CountEntriesAsync(module.ConnectionString, ct));
        foreach (var (id, playerAmount) in new[] { (DebitId(bet), -10.00m), (CreditId(bet), 25.00m), (RollbackId(bet), 10.00m) })
        {
            Assert.Equal(1, await LedgerRows.CountEntriesAsync(module.ConnectionString, id, ct));
            var amounts = await LedgerRows.PostingAmountsAsync(module.ConnectionString, id, ct);
            Assert.Equal(2, amounts.Count);
            Assert.Equal(0m, amounts.Sum());
            Assert.Contains(playerAmount, amounts);
        }

        Assert.Equal(0, await LedgerRows.CountEntriesAsync(module.ConnectionString, RollbackId(orphanBet), ct));
    }
}
