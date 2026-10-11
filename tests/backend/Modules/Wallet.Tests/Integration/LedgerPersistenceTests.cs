using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>IT-01: a debit on real PostgreSQL with the migrations of the schema <c>wallet</c> records one balanced entry (SPEC-0021 §2).</summary>
[Collection(WalletPostgres.Name)]
public sealed class LedgerPersistenceTests(PostgreSqlContainerFixture postgres)
{
    [Fact(DisplayName = "Dado PostgreSQL real, quando um jogador novo é debitado em 10,00, então o saldo é 2.990,00 e o schema wallet tem um lançamento com duas partidas que somam zero")]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealPostgresWhenNewPlayerIsDebitedThenOneBalancedEntryIsPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);
        var player = NewPlayer();
        var bet = Guid.NewGuid();

        var result = await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct));

        AssertOk(result, 2_990.00m);

        // Another "process" reads what is persisted: nothing comes from memory.
        await using var reader = await WalletServices.OpenAsync(module.ConnectionString, ct);
        Assert.Equal(BrlBalance(2_990.00m), await reader.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct)));

        Assert.Equal(1, await LedgerRows.CountEntriesAsync(module.ConnectionString, ct));
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(module.ConnectionString, DebitId(bet), ct));
        var amounts = await LedgerRows.PostingAmountsAsync(module.ConnectionString, DebitId(bet), ct);
        Assert.Equal(2, amounts.Count);
        Assert.Equal(0m, amounts.Sum());
        Assert.Contains(-10.00m, amounts);
        Assert.Contains(10.00m, amounts);
    }
}
