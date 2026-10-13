using System.Net;
using System.Text;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Logging;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure;
using Apogeu.Wallet.Infrastructure.Seamless;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Unit;

/// <summary>
/// UT-02 and UT-03 (G4 of SPEC-0046): an answer whose <c>currency</c> or <c>transactionId</c> differ from the request is
/// uncertain (Unavailable retryable, nothing mirrored); a wallet URL over plain http is a misconfiguration unless its host
/// is the loopback (Unavailable not retryable, an Error log, no call made); and the module has no background service of its
/// own resending to the operators (the resend is a scheduled Wolverine message, ADR-0002/0018).
/// </summary>
public sealed class SeamlessWalletHardeningTests : IDisposable
{
    private readonly TestApiKey _key = TestApiKey.Generate();
    private readonly LogCapture _logs = new();
    private readonly LoggerFactory _loggerFactory;
    private readonly List<HttpClient> _clients = [];

    public SeamlessWalletHardeningTests() => _loggerFactory = new LoggerFactory([_logs]);

    [Theory(DisplayName = "Dada a resposta OK do operador com currency ou transactionId diferentes do pedido, quando o débito é mapeado, então Unavailable(retryable: true) e nada vai ao espelho")]
    [Trait("Spec", "SPEC-0046:UT-02")]
    [InlineData("USD", false)]
    [InlineData("BRL", true)]
    [InlineData("USD", true)]
    public async Task GivenAnswerNotMatchingTheRequestWhenDebitIsMappedThenUncertain(string answeredCurrency, bool otherTransaction)
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var answeredId = otherTransaction ? DebitId(Guid.NewGuid()).Value : debit.TransactionId.Value;
        var ledger = new InMemoryWalletLedger();
        var wallet = Create(new Uri("https://operator.test"), ledger, (_, _) => Task.FromResult(Answer("OK", "2990.00", answeredCurrency, answeredId)));

        var result = await wallet.DebitAsync(debit, ct);

        Assert.Equal(new WalletUnavailable(Retryable: true), result);
        Assert.Null(await ledger.FindAsync(debit.TransactionId, ct));
    }

    [Theory(DisplayName = "Dada uma walletUrl http para um host que não é loopback, quando a Apogeu debita, então Unavailable(retryable: false), log Error e nenhuma chamada sai")]
    [Trait("Spec", "SPEC-0046:UT-02")]
    [InlineData("http://operator.test")]
    [InlineData("http://10.0.0.5:8080")]
    public async Task GivenPlainHttpWalletUrlToRemoteHostWhenDebitingThenRefusedAsMisconfiguration(string walletUrl)
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var calls = 0;
        var wallet = Create(new Uri(walletUrl), new InMemoryWalletLedger(), (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Answer("OK", "2990.00", "BRL", debit.TransactionId.Value));
        });

        var result = await wallet.DebitAsync(debit, ct);

        Assert.Equal(new WalletUnavailable(Retryable: false), result);
        Assert.Equal(0, calls);
        Assert.Contains(_logs.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.DoesNotContain(_logs.Entries, entry => entry.Message.Contains(_key.Secret, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Dada uma walletUrl http para o loopback (127.0.0.1), quando a Apogeu debita, então a chamada é feita e Ok volta")]
    [Trait("Spec", "SPEC-0046:UT-02")]
    public async Task GivenPlainHttpWalletUrlToLoopbackWhenDebitingThenAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var wallet = Create(new Uri("http://127.0.0.1:8080"), new InMemoryWalletLedger(), (_, _) =>
            Task.FromResult(Answer("OK", "2990.00", "BRL", debit.TransactionId.Value)));

        var result = await wallet.DebitAsync(debit, ct);

        AssertOk(result, 2_990.00m);
    }

    [Fact(DisplayName = "Dado o Wallet.Infrastructure, quando inspecionado, então não há serviço em segundo plano próprio (o reenvio é mensagem agendada do Wolverine)")]
    [Trait("Spec", "SPEC-0046:UT-03")]
    public void GivenWalletInfrastructureWhenInspectedThenItHasNoHostedService()
    {
        var hosted = typeof(WalletInfrastructure).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IHostedService).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(hosted);
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _loggerFactory.Dispose();
        _logs.Dispose();
    }

    private static HttpResponseMessage Answer(string status, string balance, string currency, string transactionId) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"status":"{{status}}","balance":"{{balance}}","currency":"{{currency}}","transactionId":"{{transactionId}}"}""",
                Encoding.UTF8,
                "application/json"),
        };

    private SeamlessOperatorWallet Create(
        Uri walletUrl, InMemoryWalletLedger ledger, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var client = new HttpClient(new ScriptedHandler(respond));
        _clients.Add(client);
        var settings = new FixedOperatorWalletSettings().UseSeamless(Casino, walletUrl, _key);
        return new SeamlessOperatorWallet(client, settings, ledger, TimeProvider.System, _loggerFactory.CreateLogger<SeamlessOperatorWallet>());
    }

    /// <summary>The HTTP boundary of the operator, answering as the test scripts it.</summary>
    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
