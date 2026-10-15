using System.Diagnostics;
using Apogeu.Operators.Contracts;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-05 (SPEC-0046 §5): 300 sequential seamless debits with the operator answering in 100 ms, p99 ≤ 300 ms, on real
/// PostgreSQL (mirror ledger) and real HTTP. Timed, so it runs in a collection without parallelism.
/// </summary>
[Collection(TimedWalletPostgres.Name)]
public sealed class SeamlessDebitPerformanceTests(PostgreSqlContainerFixture postgres)
{
    private const int WarmUps = 20;
    private const int Samples = 300;
    private static readonly TimeSpan OperatorLatency = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(300);

    [Fact(DisplayName = "Dado o operador respondendo em 100 ms, quando 300 débitos seamless são medidos, então p99 ≤ 300 ms")]
    [Trait("Spec", "SPEC-0046:IT-05")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenOperatorAnsweringInOneHundredMillisecondsWhenThreeHundredDebitsAreMeasuredThenP99IsAtMostThreeHundredMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        stub.Delay = OperatorLatency;
        var key = TestApiKey.Generate();
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct, services =>
        {
            services.RemoveAll<IOperatorWalletSettings>();
            services.AddSingleton<IOperatorWalletSettings>(SeamlessOn(stub, key));
        });
        var wallet = module.Provider.GetRequiredService<IOperatorWallet>();
        var player = NewCasinoPlayer();
        var balance = OperatorWalletStub.OpeningBalance;

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
            p99 <= Budget.TotalMilliseconds,
            $"Expected the seamless debit p99 to be at most {Budget.TotalMilliseconds} ms with the operator at {OperatorLatency.TotalMilliseconds} ms (SPEC-0046 §5), measured {p99:F2} ms (median {samples[Samples / 2]:F2} ms).");
    }
}
