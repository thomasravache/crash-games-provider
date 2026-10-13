using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Tests.Support;
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
/// Second G4 of SPEC-0046 (Emenda 2026-10-13). IT-08: the same credit asked again and again by its caller (the durable
/// retries of Betting) while the operator is down keeps one resend chain per transactionId, so the calls to the operator stay
/// within the backoff of one chain, and it is applied once when the operator is back. UT-04: a credit or rollback whose first
/// attempt is <c>not_configured</c> and is left pending answers Unavailable(retryable: true); a debit stays not retryable.
/// Both on the real Host (Wolverine with its PostgreSQL message store), where the pending resend is actually scheduled.
/// </summary>
[Collection(WalletApiDependencies.Name)]
public sealed class SeamlessResendChainTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    /// <summary>How many times the caller asks for the same credit (the durable retries of Betting).</summary>
    private const int Requests = 10;

    /// <summary>Window in which the calls to the operator are counted.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Resends one chain can make in <see cref="Window"/> with the backoff 1 s, 2 s, 4 s, 8 s … (at 1, 3, 7 and 15 s), with a
    /// margin for the scheduling: anything above means more than one chain for the transaction.
    /// </summary>
    private const int OneChainResends = 6;

    [Fact(DisplayName = "Dado o operador fora do ar, quando o mesmo crédito (mesmo transactionId) é pedido 10 vezes, então há uma única cadeia de reenvio (chamadas ao operador limitadas ao backoff de uma cadeia) e, quando o operador volta, é aplicado uma vez")]
    [Trait("Spec", "SPEC-0046:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenOperatorDownWhenSameCreditIsAskedManyTimesThenOneResendChainAndAppliedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var down = 1;
        stub.FailWhen = _ => Volatile.Read(ref down) == 1;
        var key = TestApiKey.Generate();
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        await using var host = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(SeamlessOn(stub, key)));
        var credit = Credit(NewCasinoPlayer(), 25.00m, Guid.NewGuid());
        var id = credit.TransactionId.Value;

        var start = DateTimeOffset.UtcNow;
        for (var request = 0; request < Requests; request++)
        {
            var result = await host.WithWalletAsync(wallet => wallet.CreditAsync(credit, ct));
            Assert.False(result is WalletOk or WalletDuplicate, $"The operator is down; request {request + 1} got {result}.");
        }

        await Task.Delay(start + Window - DateTimeOffset.UtcNow, ct);
        var inWindow = stub.Received.Count(call => string.Equals(call.TransactionId, id, StringComparison.Ordinal) && call.ReceivedAt <= start + Window);
        Assert.True(
            inWindow <= Requests + OneChainResends,
            $"{inWindow} calls to the operator in {Window} for {Requests} requests of {id}: more than one resend chain (at most {Requests + OneChainResends}; answers: {Describe(stub.CallsOf(id))}).");

        Volatile.Write(ref down, 0);
        await Eventually.HoldsAsync(
            () => stub.AppliedCount(id) == 1,
            TimeSpan.FromMinutes(1),
            () => $"the pending credit {id} applied after the operator came back (answers: {Describe(stub.CallsOf(id))})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        Assert.Equal(1, stub.AppliedCount(id));
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(database, credit.TransactionId, ct));
    }

    [Fact(DisplayName = "Dado um crédito ou estorno cuja 1ª tentativa dá not_configured e fica pendente, quando a carteira responde, então Unavailable(retryable: true) e a pendência é entregue quando a configuração volta; o débito continua Unavailable(retryable: false)")]
    [Trait("Spec", "SPEC-0046:UT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenNotConfiguredFirstAttemptWhenCreditOrRollbackIsPendingThenRetryable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var key = TestApiKey.Generate();
        var settings = SeamlessOn(stub, key);
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        await using var host = await WalletApiHost.StartOnDatabaseAsync(database, redis.ConnectionString, UseSettings(settings));
        var player = NewCasinoPlayer();
        var debitedBet = Guid.NewGuid();
        AssertOk(await host.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, debitedBet), ct)), OperatorWalletStub.OpeningBalance - 10.00m);

        // The operator is seamless without a wallet URL nor key: every first attempt is not_configured.
        settings.UseSeamlessWithoutWallet(Casino);
        var credit = Credit(player, 25.00m, Guid.NewGuid());
        var rollback = Rollback(player, debitedBet);

        var credited = await host.WithWalletAsync(wallet => wallet.CreditAsync(credit, ct));
        var rolledBack = await host.WithWalletAsync(wallet => wallet.RollbackAsync(rollback, ct));
        var debited = await host.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, Guid.NewGuid()), ct));

        Assert.Equal(new WalletUnavailable(Retryable: true), credited);
        Assert.Equal(new WalletUnavailable(Retryable: true), rolledBack);
        Assert.Equal(new WalletUnavailable(Retryable: false), debited);

        // They were really left pending: once the configuration is back, both are delivered, once.
        settings.UseSeamless(Casino, stub.BaseUrl, key);
        await Eventually.HoldsAsync(
            () => stub.AppliedCount(credit.TransactionId.Value) == 1 && stub.AppliedCount(rollback.TransactionId.Value) == 1,
            TimeSpan.FromMinutes(1),
            () => $"the pending credit and rollback delivered (credit: {Describe(stub.CallsOf(credit.TransactionId.Value))}; rollback: {Describe(stub.CallsOf(rollback.TransactionId.Value))})");
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
