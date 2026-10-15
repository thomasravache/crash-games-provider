using System.Diagnostics;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Performance;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-05: 500 sequential demo debits on real PostgreSQL with p99 ≤ 20 ms (SPEC-0021 §5, within the 100 ms bet budget of
/// ADR-0019). Timed, so it runs in a collection without parallelism.
/// </summary>
[Collection(TimedWalletPostgres.Name)]
public sealed class DebitPerformanceTests(PostgreSqlContainerFixture postgres)
{
    private const int WarmUps = 20;
    private const int Samples = 500;
    // 20 ms on the reference machine, × the machine factor elsewhere (SPEC-0052).
    private static readonly (TimeSpan Budget, string Source) Budget =
        PerformanceBudget.Resolve(TimeSpan.FromMilliseconds(20), Environment.GetEnvironmentVariable);

    [Fact(DisplayName = "Dados 500 débitos sequenciais com Postgres real, quando medidos, então p99 ≤ 20 ms na máquina de referência")]
    [Trait("Spec", "SPEC-0021:IT-05")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenFiveHundredSequentialDebitsWhenMeasuredThenP99IsAtMostTwentyMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);
        var wallet = module.Provider.GetRequiredService<IOperatorWallet>();
        var player = NewPlayer();
        var balance = OpeningBalance;

        for (var i = 0; i < WarmUps; i++)
        {
            balance -= 1.00m;
            AssertOk(await wallet.DebitAsync(Debit(player, 1.00m, Guid.NewGuid()), ct), balance);
        }

        var samples = new double[Samples];
        for (var i = 0; i < Samples; i++)
        {
            var debit = Debit(player, 1.00m, Guid.NewGuid());
            var watch = Stopwatch.StartNew();
            var result = await wallet.DebitAsync(debit, ct);
            watch.Stop();
            samples[i] = watch.Elapsed.TotalMilliseconds;
            balance -= 1.00m;
            AssertOk(result, balance);
        }

        Array.Sort(samples);
        var p99 = samples[(int)Math.Ceiling(Samples * 0.99) - 1];
        Assert.True(
            p99 <= Budget.Budget.TotalMilliseconds,
            $"Expected the demo debit p99 to be at most {Budget.Budget.TotalMilliseconds} ms from {Budget.Source} (SPEC-0021 §5), measured {p99:F2} ms (median {samples[Samples / 2]:F2} ms).");
    }
}
