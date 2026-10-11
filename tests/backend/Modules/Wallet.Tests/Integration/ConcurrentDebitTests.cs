using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>IT-02: idempotency by transaction id under concurrency on real PostgreSQL (SPEC-0021 §5).</summary>
[Collection(WalletPostgres.Name)]
public sealed class ConcurrentDebitTests(PostgreSqlContainerFixture postgres)
{
    private const int Parallel = 20;

    [Fact(DisplayName = "Dados 20 débitos com o mesmo TransactionId em paralelo, quando executados, então existe um único lançamento e todos recebem o mesmo resultado")]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenTwentyParallelDebitsWithSameTransactionIdWhenExecutedThenSingleEntryAndSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);
        var player = NewPlayer();
        var bet = Guid.NewGuid();
        var debit = Debit(player, 10.00m, bet);
        using var start = new ManualResetEventSlim(initialState: false);

        var calls = Enumerable.Range(0, Parallel)
            .Select(_ => Task.Run(
                async () =>
                {
                    start.Wait(ct);
                    return await module.WithWalletAsync(wallet => wallet.DebitAsync(debit, ct)).ConfigureAwait(false);
                },
                ct))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(calls);

        // Exactly one effect: one Ok, every other call a Duplicate carrying that same original result.
        var ok = Assert.Single(results.OfType<WalletOk>());
        Assert.Equal(BrlBalance(2_990.00m), ok.BalanceAfter);
        Assert.All(results.Where(result => result is not WalletOk), result => AssertDuplicateOfOk(result, 2_990.00m));

        Assert.Equal(1, await LedgerRows.CountEntriesAsync(module.ConnectionString, ct));
        Assert.Equal(2, await LedgerRows.CountPostingsAsync(module.ConnectionString, ct));
        Assert.Equal(BrlBalance(2_990.00m), await module.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct)));
    }
}
