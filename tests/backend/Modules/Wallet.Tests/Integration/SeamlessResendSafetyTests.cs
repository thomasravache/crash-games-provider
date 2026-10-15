using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Logging;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// G4 of SPEC-0046 (Emenda 2026-10-13): the durable resend of credits never drops a prize and is not serialized across
/// operators. IT-06: a pending credit survives its operator losing the configuration or switching to demo, and is delivered
/// once, with the same transactionId, when the configuration comes back. IT-07: one operator timing out and one whose
/// settings lookup throws delay no other operator's pending credit, and the throwing one is still delivered afterwards.
/// UT-02 (redirect): a 307/308 of the operator is uncertain and not followed. Driven only through the Host's
/// <see cref="IOperatorWallet"/> (real PostgreSQL and Redis), so they hold whatever carries the resend.
/// </summary>
[Collection(WalletApiDependencies.Name)]
public sealed class SeamlessResendSafetyTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    /// <summary>Longer than the 5 s deadline of a credit attempt: the call times out.</summary>
    private static readonly TimeSpan BeyondDeadline = TimeSpan.FromSeconds(7);

    /// <summary>How long a pending credit of a healthy operator may take to be confirmed by its first resend.</summary>
    private static readonly TimeSpan HealthyResendBound = TimeSpan.FromSeconds(12);

    [Theory(DisplayName = "Dado um crédito pendente (timeout), quando a configuração do operador some ou o modo vira demo, então a pendência não é descartada (log Error) e, quando a configuração volta, é entregue uma única vez com o mesmo transactionId")]
    [Trait("Spec", "SPEC-0046:IT-06")]
    [Trait("Category", "Integration")]
    [InlineData("forgotten")]
    [InlineData("demo")]
    public async Task GivenPendingCreditWhenOperatorConfigurationIsLostThenItIsKeptAndDeliveredOnceWhenItComesBack(string change)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var down = 1;
        stub.Delay = BeyondDeadline;
        stub.FailWhen = _ => Volatile.Read(ref down) == 1;
        var key = TestApiKey.Generate();
        var settings = SeamlessOn(stub, key);
        using var logs = new LogCapture();
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        await using var host = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(settings, logs));
        var player = NewCasinoPlayer();
        var bet = Guid.NewGuid();
        var credit = CreditId(bet).Value;

        var pending = host.WithWalletAsync(wallet => wallet.CreditAsync(Credit(player, 25.00m, bet), CancellationToken.None));
        await Eventually.HoldsAsync(
            () => stub.Received.Any(request => string.Equals(request.TransactionId, credit, StringComparison.Ordinal)),
            TimeSpan.FromSeconds(20),
            () => $"the credit {credit} attempted at the operator");

        // The operator configuration goes away while the credit is pending (its attempt times out).
        _ = change switch
        {
            "forgotten" => settings.Forget(Casino),
            _ => settings.UseDemo(Casino),
        };
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(30), ct);
        Assert.False(result is WalletOk or WalletDuplicate, $"The operator never confirmed the credit; got {result}.");

        // Without configuration the prize is neither delivered elsewhere nor dropped; the operation is reported as an error.
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        Assert.Equal(0, stub.AppliedCount(credit));
        Assert.Contains(logs.Entries, entry => entry.Level >= LogLevel.Error
            && (entry.Message.Contains(credit, StringComparison.Ordinal) || entry.Message.Contains(Casino.Value, StringComparison.Ordinal)));

        // The configuration comes back and the operator is up: the same pending credit is delivered, once.
        stub.Delay = TimeSpan.Zero;
        Volatile.Write(ref down, 0);
        settings.UseSeamless(Casino, stub.BaseUrl, key);

        await Eventually.HoldsAsync(
            () => stub.AppliedCount(credit) == 1,
            TimeSpan.FromMinutes(2),
            () => $"the pending credit {credit} delivered after the configuration came back (answers: {Describe(stub.CallsOf(credit))})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        Assert.Equal(1, stub.AppliedCount(credit));
        Assert.All(stub.CallsOf(credit), call => Assert.Equal(CreditPath, call.Request.Path));
        Assert.All(stub.CallsOf(credit), call => AssertSigned(call.Request, key));
        Assert.Equal(OperatorWalletStub.OpeningBalance + 25.00m, stub.BalanceOf(player.PlayerId.Value, "BRL"));

        // The mirror ledger holds the credit once, with the operator's balance (never a demo credit).
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(database, CreditId(bet), ct));
        var entry = await host.Services.GetRequiredService<IWalletLedger>().FindAsync(CreditId(bet), ct);
        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryKind.Credit, entry.Kind);
        Assert.Equal(OperatorWalletStub.OpeningBalance + 25.00m, entry.BalanceAfter.Value);
    }

    [Fact(DisplayName = "Dados um operador que sempre estoura o prazo e outro cuja configuração falha, quando um terceiro operador saudável tem um crédito pendente, então ele é confirmado em poucos segundos; o crédito do operador com falha é entregue depois, uma vez")]
    [Trait("Spec", "SPEC-0046:IT-07")]
    [Trait("Category", "Integration")]
    public async Task GivenSlowAndFailingOperatorsWhenHealthyOperatorHasPendingCreditThenItIsNotBlocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var slowOperator = new OperatorId("casino-slow");
        var healthyOperator = new OperatorId("casino-healthy");
        var failingOperator = new OperatorId("casino-failing");
        await using var slow = await OperatorWalletStub.StartAsync(ct);
        slow.Delay = BeyondDeadline;
        slow.FailWhen = _ => true;
        await using var healthy = await FailingOnceAsync(ct);
        await using var failing = await FailingOnceAsync(ct);
        var key = TestApiKey.Generate();
        var settings = new FixedOperatorWalletSettings()
            .UseSeamless(slowOperator, slow.BaseUrl, key)
            .UseSeamless(healthyOperator, healthy.BaseUrl, key)
            .UseSeamless(failingOperator, failing.BaseUrl, key);
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        await using var host = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(settings, logs: null));

        // Several credits of the slow operator are pending; every attempt of theirs takes the whole deadline.
        var slowCredits = Enumerable.Range(0, 4)
            .Select(_ => Credit(PlayerOf(slowOperator), 10.00m, Guid.NewGuid()))
            .ToList();
        var slowCalls = slowCredits
            .Select(op => host.WithWalletAsync(wallet => wallet.CreditAsync(op, CancellationToken.None)))
            .ToList();
        await Eventually.HoldsAsync(
            () => slowCredits.All(op => slow.Received.Any(request => string.Equals(request.TransactionId, op.TransactionId.Value, StringComparison.Ordinal))),
            TimeSpan.FromSeconds(20),
            () => "the credits of the slow operator attempted");
        await Task.WhenAll(slowCalls).WaitAsync(TimeSpan.FromSeconds(30), ct);
        await Task.Delay(TimeSpan.FromSeconds(1.5), ct);

        // A credit of the failing operator goes pending, then its settings lookup starts throwing.
        var failingCredit = await PendingCreditAsync(host, failingOperator, 15.00m);
        settings.FailLookup(failingOperator, failing: true);

        // A credit of the healthy operator fails once and is pending: its resend must not wait behind the others.
        var healthyCredit = await PendingCreditAsync(host, healthyOperator, 25.00m);
        var healthyId = healthyCredit.TransactionId.Value;

        await Eventually.HoldsAsync(
            () => healthy.AppliedCount(healthyId) == 1,
            HealthyResendBound,
            () => $"the pending credit {healthyId} of the healthy operator confirmed without waiting for the slow and the failing operators (answers: {Describe(healthy.CallsOf(healthyId))}; slow attempts so far: {slow.Received.Count})");

        // The failing operator recovers: its pending credit was kept and is delivered, once.
        settings.FailLookup(failingOperator, failing: false);
        var failingId = failingCredit.TransactionId.Value;
        await Eventually.HoldsAsync(
            () => failing.AppliedCount(failingId) == 1,
            TimeSpan.FromMinutes(2),
            () => $"the pending credit {failingId} delivered after the settings of its operator recovered (answers: {Describe(failing.CallsOf(failingId))})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        Assert.Equal(1, healthy.AppliedCount(healthyId));
        Assert.Equal(1, failing.AppliedCount(failingId));
        Assert.All(slowCredits, op => Assert.Equal(0, slow.AppliedCount(op.TransactionId.Value)));
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(database, healthyCredit.TransactionId, ct));
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(database, failingCredit.TransactionId, ct));
    }

    [Theory(DisplayName = "Dado o operador respondendo com redirecionamento 307/308, quando a Apogeu debita, então Unavailable(retryable) e o redirecionamento não é seguido (o destino não recebe nada)")]
    [Trait("Spec", "SPEC-0046:UT-02")]
    [Trait("Category", "Integration")]
    [InlineData(307)]
    [InlineData(308)]
    public async Task GivenRedirectingOperatorWhenDebitingThenUncertainAndRedirectNotFollowed(int redirectStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var target = await OperatorWalletStub.StartAsync(ct);
        await using var redirecting = await RedirectingWalletServer.StartAsync(target.BaseUrl, redirectStatus, ct);
        var key = TestApiKey.Generate();
        var settings = new FixedOperatorWalletSettings().UseSeamless(Casino, redirecting.BaseUrl, key);
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        await using var host = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(settings, logs: null));
        var bet = Guid.NewGuid();

        var result = await host.WithWalletAsync(wallet => wallet.DebitAsync(Debit(NewCasinoPlayer(), 10.00m, bet), ct));

        Assert.Equal(new WalletUnavailable(Retryable: true), result);
        Assert.True(redirecting.Hits >= 1, "The debit was sent to the configured wallet URL.");
        Assert.Empty(target.Received);
        Assert.Equal(0, await LedgerRows.CountEntriesAsync(database, DebitId(bet), ct));
    }

    /// <summary>An operator wallet that answers 500 to its first call only.</summary>
    private static async Task<OperatorWalletStub> FailingOnceAsync(CancellationToken ct)
    {
        var stub = await OperatorWalletStub.StartAsync(ct).ConfigureAwait(false);
        var failures = 1;
        stub.FailWhen = _ => Interlocked.Decrement(ref failures) >= 0;
        return stub;
    }

    /// <summary>A credit for a new player of <paramref name="op"/> whose first attempt is not confirmed (the operator answers 500).</summary>
    private static async Task<WalletOperation> PendingCreditAsync(WalletApiHost host, OperatorId op, decimal amount)
    {
        var credit = Credit(PlayerOf(op), amount, Guid.NewGuid());
        var result = await host.WithWalletAsync(wallet => wallet.CreditAsync(credit, CancellationToken.None)).ConfigureAwait(false);
        Assert.False(result is WalletOk or WalletDuplicate, $"The first attempt for {op.Value} answered 500; got {result}.");
        return credit;
    }

    private static PlayerRef PlayerOf(OperatorId op) => new(op, new PlayerId("p-" + Guid.NewGuid().ToString("N")));

    private static Action<IServiceCollection> UseSettings(FixedOperatorWalletSettings settings, LogCapture? logs) => services =>
    {
        services.RemoveAll<IOperatorWalletSettings>();
        services.AddSingleton<IOperatorWalletSettings>(settings);
        if (logs is not null)
        {
            services.AddLogging(logging => logging.AddProvider(logs));
        }
    };

    private static string Describe(IEnumerable<StubCall> calls) =>
        string.Join(", ", calls.Select(call => $"{call.Request.ReceivedAt:HH:mm:ss.fff} {call.StatusCode} {call.Status}"));
}
