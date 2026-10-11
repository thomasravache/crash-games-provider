using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-08: on real PostgreSQL a debit is refunded once, whatever the ids of its rollbacks (SPEC-0021 §6): two rollbacks
/// with distinct, non-deterministic ids, in sequence and concurrently, give one Rollback entry, one Ok and one Duplicate.
/// </summary>
[Collection(WalletPostgres.Name)]
public sealed class SingleRefundTests(PostgreSqlContainerFixture postgres)
{
    private const int Iterations = 20;

    [Fact(DisplayName = "Dado um débito de 10,00 no PostgreSQL real, quando chegam dois estornos com ids distintos não determinísticos (em sequência e concorrentes, 20 vezes), então um único lançamento Rollback, um Ok e um Duplicate do primeiro, e o saldo sobe 10,00 uma vez")]
    [Trait("Spec", "SPEC-0021:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenDebitWhenTwoRollbacksWithDistinctIdsArriveThenRefundOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);

        foreach (var concurrent in new[] { false, true })
        {
            for (var iteration = 0; iteration < Iterations; iteration++)
            {
                var player = NewPlayer();
                var bet = Guid.NewGuid();
                AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct)), 2_990.00m);
                var template = Rollback(player, bet);
                var first = template with { TransactionId = new TransactionId("refund:" + Guid.NewGuid().ToString("N")) };
                var second = template with { TransactionId = new TransactionId("refund:" + Guid.NewGuid().ToString("N")) };

                WalletResult[] results;
                if (concurrent)
                {
                    using var start = new ManualResetEventSlim(initialState: false);
                    var calls = new[] { first, second }
                        .Select(rollback => Task.Run(
                            async () =>
                            {
                                start.Wait(ct);
                                return await module.WithWalletAsync(wallet => wallet.RollbackAsync(rollback, ct)).ConfigureAwait(false);
                            },
                            ct))
                        .ToArray();
                    start.Set();
                    results = await Task.WhenAll(calls);
                }
                else
                {
                    results =
                    [
                        await module.WithWalletAsync(wallet => wallet.RollbackAsync(first, ct)),
                        await module.WithWalletAsync(wallet => wallet.RollbackAsync(second, ct)),
                    ];
                }

                var label = (concurrent ? "concurrent" : "sequential") + " iteration " + iteration;
                var ok = Assert.Single(results.OfType<WalletOk>());
                Assert.Equal(BrlBalance(OpeningBalance), ok.BalanceAfter);
                var duplicate = Assert.Single(results.OfType<WalletDuplicate>());
                Assert.True(duplicate.OriginalResult == ok, $"{label}: the duplicate must carry the first refund's result, got {duplicate.OriginalResult}.");
                if (!concurrent)
                {
                    Assert.IsType<WalletOk>(results[0]);
                }

                var refunds = await LedgerRows.CountEntriesAsync(module.ConnectionString, first.TransactionId, ct)
                    + await LedgerRows.CountEntriesAsync(module.ConnectionString, second.TransactionId, ct);
                Assert.True(refunds == 1, $"{label}: expected exactly one Rollback entry for the debit, found {refunds}.");
                Assert.Equal(BrlBalance(OpeningBalance), await module.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct)));
            }
        }
    }
}
