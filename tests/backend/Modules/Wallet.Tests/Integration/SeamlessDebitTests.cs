using System.Diagnostics;
using Apogeu.Operators.Contracts;
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
/// IT-01 and IT-02 (SPEC-0046 §7.3): the module's <see cref="IOperatorWallet"/> (as <c>AddWalletInfrastructure</c> composes it,
/// on real PostgreSQL) for an operator in seamless mode talks to the operator test server over real HTTP; a demo operator
/// keeps the demo adapter.
/// </summary>
[Collection(WalletPostgres.Name)]
public sealed class SeamlessDebitTests(PostgreSqlContainerFixture postgres)
{
    [Fact(DisplayName = "Dado um operador de teste HTTP real em modo seamless, quando a Apogeu debita, então a chamada vai assinada, Ok volta com o saldo do operador e o livro-razão espelho registra o débito; o segredo nunca é logado e o operador demo continua demo")]
    [Trait("Spec", "SPEC-0046:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenSeamlessOperatorWhenDebitingThenSignedCallAndOkWithOperatorBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var key = TestApiKey.Generate();
        using var logs = new LogCapture();
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct, services =>
        {
            services.RemoveAll<IOperatorWalletSettings>();
            services.AddSingleton<IOperatorWalletSettings>(SeamlessOn(stub, key));
            services.AddLogging(logging => logging.AddProvider(logs));
        });
        var player = NewCasinoPlayer();
        var bet = Guid.NewGuid();

        var result = await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct));

        AssertOk(result, 2_990.00m);
        var call = Assert.Single(stub.Calls);
        Assert.Equal(DebitPath, call.Request.Path);
        Assert.Equal(DebitId(bet).Value, call.Request.TransactionId);
        AssertSigned(call.Request, key);
        Assert.Equal(2_990.00m, stub.BalanceOf(player.PlayerId.Value, "BRL"));

        // Mirror ledger: one entry of the debit with the operator's answer.
        var ledger = module.Provider.GetRequiredService<IWalletLedger>();
        var entry = await ledger.FindAsync(DebitId(bet), ct);
        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryKind.Debit, entry.Kind);
        Assert.Equal(2_990.00m, entry.BalanceAfter.Value);
        AssertBalancedEntry(entry, player, -10.00m);
        Assert.Equal(1, await LedgerRows.CountEntriesAsync(module.ConnectionString, DebitId(bet), ct));

        // The secret of the key never reaches the logs (ADR-0014).
        Assert.DoesNotContain(logs.Entries, log => log.Message.Contains(key.Secret, StringComparison.Ordinal)
            || (log.Exception?.ToString().Contains(key.Secret, StringComparison.Ordinal) ?? false));

        // An operator without wallet configuration stays demo (default walletMode demo): no call to the operator server.
        var demoPlayer = NewPlayer();
        AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(demoPlayer, 10.00m, Guid.NewGuid()), ct)), OpeningBalance - 10.00m);
        Assert.Single(stub.Received);
    }

    [Fact(DisplayName = "Dado o operador demorando 3 s, quando o débito é feito, então Unavailable(retryable) no prazo de 2 s, sem lançamento no espelho e sem reenvio do débito (a aposta não é aceita, SPEC-0022)")]
    [Trait("Spec", "SPEC-0046:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenSlowOperatorWhenDebitingThenUnavailableWithinDeadlineAndNotResent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        stub.Delay = TimeSpan.FromSeconds(3);
        var key = TestApiKey.Generate();
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct, services =>
        {
            services.RemoveAll<IOperatorWalletSettings>();
            services.AddSingleton<IOperatorWalletSettings>(SeamlessOn(stub, key));
        });
        var player = NewCasinoPlayer();
        var bet = Guid.NewGuid();

        var watch = Stopwatch.StartNew();
        var result = await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct));
        watch.Stop();

        // Unavailable(retryable) is what PlaceBet (SPEC-0022) refuses with WALLET_UNAVAILABLE, then reverses the debit.
        Assert.Equal(new WalletUnavailable(Retryable: true), result);
        Assert.InRange(watch.Elapsed, DebitDeadline - TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(2.9));
        Assert.Equal(0, await LedgerRows.CountEntriesAsync(module.ConnectionString, DebitId(bet), ct));

        // The debit is never resent by the adapter.
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        Assert.Single(stub.Received, request => string.Equals(request.TransactionId, DebitId(bet).Value, StringComparison.Ordinal));
    }
}
