using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-03 and IT-04 (SPEC-0046 §7.3): a credit with the operator down is resent with the same transactionId, with exponential
/// backoff (1 s … 5 min), until confirmed, applied once — also after the process restarts. Real Host (WebApplicationFactory)
/// on real PostgreSQL and Redis, so the module's own resend (pending table and sweep) runs as in production.
/// </summary>
[Collection(WalletApiDependencies.Name)]
public sealed class SeamlessCreditRetryTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private static readonly TimeSpan Outage = TimeSpan.FromMinutes(1);

    [Fact(DisplayName = "Dado o operador respondendo 500 por 1 min, quando um crédito é feito, então é reenviado com backoff até OK, com o mesmo transactionId, e aplicado uma única vez")]
    [Trait("Spec", "SPEC-0046:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenOperatorFailingForOneMinuteWhenCreditingThenResentWithBackoffUntilOkAndAppliedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var key = TestApiKey.Generate();
        var outageEnds = DateTimeOffset.UtcNow + Outage;
        stub.FailWhen = request => string.Equals(request.Path, CreditPath, StringComparison.Ordinal) && request.ReceivedAt < outageEnds;
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        await using var host = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(SeamlessOn(stub, key)));
        var player = NewCasinoPlayer();
        var bet = Guid.NewGuid();
        var credit = CreditId(bet).Value;

        var result = await host.WithWalletAsync(wallet => wallet.CreditAsync(Credit(player, 25.00m, bet), ct));

        Assert.True(
            result is WalletOk or WalletUnavailable { Retryable: true },
            $"A credit with the operator down is either confirmed after the resends or left pending to resend (retryable); got {result}.");
        await Eventually.HoldsAsync(
            () => stub.AppliedCount(credit) == 1,
            TimeSpan.FromMinutes(3),
            () => $"the credit {credit} confirmed by the operator (answers: {Describe(stub.CallsOf(credit))})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        var calls = stub.CallsOf(credit);
        Assert.Equal(1, stub.AppliedCount(credit));
        Assert.All(calls, call => AssertSigned(call.Request, key));
        Assert.All(calls, call => Assert.Equal(CreditPath, call.Request.Path));

        // Resent until confirmed, then never again: the last answer is the only OK and nothing follows it.
        Assert.Equal("OK", calls[^1].Status);
        var failures = calls.Take(calls.Count - 1).ToList();
        Assert.All(failures, call => Assert.Equal(500, call.StatusCode));
        Assert.True(failures.Count >= 3, $"Expected several resends during the 1 min outage, got {failures.Count} ({Describe(calls)}).");

        // Exponential backoff from 1 s: the operator is not hammered and the gaps grow.
        var gaps = calls.Zip(calls.Skip(1), (a, b) => b.Request.ReceivedAt - a.Request.ReceivedAt).ToList();
        Assert.True(gaps[0] >= TimeSpan.FromMilliseconds(500), $"First resend after {gaps[0]}; the backoff starts at 1 s ({Describe(calls)}).");
        Assert.True(gaps[^1] >= gaps[0] * 2, $"The gaps between resends must grow (exponential backoff): {string.Join(", ", gaps)}.");
        Assert.All(gaps, gap => Assert.True(gap <= TimeSpan.FromMinutes(5.5), $"A gap of {gap} exceeds the 5 min cap of the backoff."));
        Assert.True(failures.Count <= 12, $"{failures.Count} attempts in 1 min is not an exponential backoff from 1 s ({Describe(calls)}).");

        // Mirror ledger: the credit recorded once, with the operator's balance.
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(database, CreditId(bet), ct));
        var entry = await host.Services.GetRequiredService<IWalletLedger>().FindAsync(CreditId(bet), ct);
        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryKind.Credit, entry.Kind);
        Assert.Equal(stub.BalanceOf(player.PlayerId.Value, "BRL"), entry.BalanceAfter.Value);
        Assert.Equal(OperatorWalletStub.OpeningBalance + 25.00m, entry.BalanceAfter.Value);
    }

    [Fact(DisplayName = "Dado um crédito pendente e o processo reiniciado, quando a varredura roda, então o crédito é confirmado pelo novo processo e aplicado uma única vez")]
    [Trait("Spec", "SPEC-0046:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenPendingCreditAndRestartedProcessWhenSweepRunsThenCreditIsConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var down = 1;
        stub.FailWhen = _ => Volatile.Read(ref down) == 1;
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        var player = NewCasinoPlayer();
        var bet = Guid.NewGuid();
        var credit = CreditId(bet).Value;

        // First process: the key of the first process signs its calls, so the confirming call can be told apart.
        var firstKey = TestApiKey.Generate();
        var first = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(SeamlessOn(stub, firstKey)));
        Task<WalletResult> pending;
        try
        {
            pending = first.WithWalletAsync(wallet => wallet.CreditAsync(Credit(player, 25.00m, bet), CancellationToken.None));
            await Eventually.HoldsAsync(
                () => stub.CallsOf(credit).Count >= 2,
                TimeSpan.FromSeconds(30),
                () => $"the credit {credit} attempted and resent by the first process (answers: {Describe(stub.CallsOf(credit))})");
        }
        finally
        {
            await first.DisposeAsync();
        }

        // The first process is gone; whatever its call returned, the credit was not confirmed.
        await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10), ct));
        if (pending.IsCompleted)
        {
            _ = pending.Exception;
        }

        Assert.Equal(0, stub.AppliedCount(credit));

        Volatile.Write(ref down, 0);
        var secondKey = TestApiKey.Generate();
        await using var second = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(SeamlessOn(stub, secondKey)));

        await Eventually.HoldsAsync(
            () => stub.AppliedCount(credit) == 1,
            TimeSpan.FromMinutes(6),
            () => $"the pending credit {credit} confirmed after the restart (answers: {Describe(stub.CallsOf(credit))})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        Assert.Equal(1, stub.AppliedCount(credit));
        var confirmed = Assert.Single(stub.CallsOf(credit), call => string.Equals(call.Status, "OK", StringComparison.Ordinal));
        AssertSigned(confirmed.Request, secondKey);
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(database, CreditId(bet), ct));
        Assert.Equal(OperatorWalletStub.OpeningBalance + 25.00m, stub.BalanceOf(player.PlayerId.Value, "BRL"));
    }

    private static Action<IServiceCollection> UseSettings(FixedOperatorWalletSettings settings) => services =>
    {
        services.RemoveAll<IOperatorWalletSettings>();
        services.AddSingleton<IOperatorWalletSettings>(settings);
    };

    private static string Describe(IEnumerable<StubCall> calls) =>
        string.Join(", ", calls.Select(call => $"{call.Request.ReceivedAt:HH:mm:ss.fff} {call.StatusCode} {call.Status}"));
}
