using System.Diagnostics;
using System.Net;
using System.Text;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Logging;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Seamless;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Unit;

/// <summary>
/// UT-01 (SPEC-0046 §6): the answers of the operator wallet mapped to the <see cref="WalletResult"/> of the contract
/// SPEC-0021@4 — OK→Ok; INSUFFICIENT_FUNDS→InsufficientFunds; DUPLICATE→Duplicate; TRANSACTION_NOT_FOUND→TransactionNotFound;
/// timeout/5xx/network (and any other answer without a status of the table) → Unavailable(retryable: true);
/// PLAYER_NOT_FOUND → Unavailable(retryable: false) with an alert. The HTTP boundary is a scripted handler; the ledger is the
/// in-memory double of <c>IWalletLedger</c>.
/// </summary>
public sealed class SeamlessWalletMappingTests : IDisposable
{
    private static readonly Uri WalletUrl = new("https://operator.test");

    private readonly TestApiKey _key = TestApiKey.Generate();
    private readonly LogCapture _logs = new();
    private readonly LoggerFactory _loggerFactory;
    private readonly List<HttpClient> _clients = [];

    public SeamlessWalletMappingTests() => _loggerFactory = new LoggerFactory([_logs]);

    public static TheoryData<int> UncertainHttpStatuses => [500, 502, 503, 401, 404];

    [Theory(DisplayName = "Dada a resposta definitiva do operador ao débito, quando mapeada, então vira o WalletResult do contrato (OK→Ok, INSUFFICIENT_FUNDS→InsufficientFunds, DUPLICATE→Duplicate do Ok original)")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    [InlineData("OK", "2990.00")]
    [InlineData("INSUFFICIENT_FUNDS", "5.00")]
    [InlineData("DUPLICATE", "2990.00")]
    public async Task GivenDefinitiveDebitAnswerWhenMappedThenItIsTheContractResult(string status, string balance)
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewCasinoPlayer();
        var debit = Debit(player, 10.00m, Guid.NewGuid());
        var requests = new List<HttpRequestMessage>();
        var wallet = Create((request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(Answer(status, balance, debit.TransactionId));
        });

        var result = await wallet.DebitAsync(debit, ct);

        WalletResult expected = status switch
        {
            "OK" => new WalletOk(BrlBalance(2_990.00m)),
            "INSUFFICIENT_FUNDS" => new WalletInsufficientFunds(BrlBalance(5.00m)),
            _ => new WalletDuplicate(new WalletOk(BrlBalance(2_990.00m))),
        };
        Assert.Equal(expected, result);
        var sent = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(new Uri(WalletUrl, DebitPath), sent.RequestUri);
    }

    [Fact(DisplayName = "Dado PLAYER_NOT_FOUND do operador, quando mapeado, então Unavailable(retryable: false) e um alerta (log ≥ Warning) é emitido")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    public async Task GivenPlayerNotFoundWhenMappedThenUnavailableNotRetryableWithAlert()
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var wallet = Create((_, _) => Task.FromResult(Answer("PLAYER_NOT_FOUND", "0.00", debit.TransactionId)));

        var result = await wallet.DebitAsync(debit, ct);

        Assert.Equal(new WalletUnavailable(Retryable: false), result);
        Assert.Contains(_logs.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.DoesNotContain(_logs.Entries, entry => entry.Message.Contains(_key.Secret, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Dado TRANSACTION_NOT_FOUND ao estorno, quando mapeado, então TransactionNotFound com o transactionId do débito original")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    public async Task GivenTransactionNotFoundOnRollbackWhenMappedThenTransactionNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var bet = Guid.NewGuid();
        var rollback = Rollback(NewCasinoPlayer(), bet);
        var requests = new List<HttpRequestMessage>();
        var wallet = Create((request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(Answer("TRANSACTION_NOT_FOUND", "3000.00", rollback.TransactionId));
        });

        var result = await wallet.RollbackAsync(rollback, ct);

        Assert.Equal(new WalletTransactionNotFound(DebitId(bet)), result);
        Assert.Equal(new Uri(WalletUrl, RollbackPath), Assert.Single(requests).RequestUri);
    }

    [Theory(DisplayName = "Dado um erro HTTP do operador (5xx ou outro não-200), quando o débito é mapeado, então Unavailable(retryable: true)")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    [MemberData(nameof(UncertainHttpStatuses))]
    public async Task GivenHttpErrorWhenDebitIsMappedThenUnavailableRetryable(int httpStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = Create((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)httpStatus)));

        var result = await wallet.DebitAsync(Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid()), ct);

        Assert.Equal(new WalletUnavailable(Retryable: true), result);
    }

    [Fact(DisplayName = "Dado 200 com status fora da tabela do contrato, quando o débito é mapeado, então Unavailable(retryable: true) (resultado incerto)")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    public async Task GivenUnknownStatusWhenDebitIsMappedThenUnavailableRetryable()
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var wallet = Create((_, _) => Task.FromResult(Answer("MAYBE", "2990.00", debit.TransactionId)));

        var result = await wallet.DebitAsync(debit, ct);

        Assert.Equal(new WalletUnavailable(Retryable: true), result);
    }

    [Fact(DisplayName = "Dada uma falha de rede, quando o débito é mapeado, então Unavailable(retryable: true)")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    public async Task GivenNetworkFailureWhenDebitIsMappedThenUnavailableRetryable()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = Create((_, _) => throw new HttpRequestException("connection refused"));

        var result = await wallet.DebitAsync(Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid()), ct);

        Assert.Equal(new WalletUnavailable(Retryable: true), result);
    }

    [Fact(DisplayName = "Dado o operador sem resposta em 3 s, quando o débito é mapeado, então Unavailable(retryable: true) no prazo de 2 s")]
    [Trait("Spec", "SPEC-0046:UT-01")]
    public async Task GivenOperatorTimeoutWhenDebitIsMappedThenUnavailableRetryableWithinDeadline()
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var wallet = Create(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            return Answer("OK", "2990.00", debit.TransactionId);
        });

        var watch = Stopwatch.StartNew();
        var result = await wallet.DebitAsync(debit, ct);
        watch.Stop();

        Assert.Equal(new WalletUnavailable(Retryable: true), result);
        Assert.InRange(watch.Elapsed, DebitDeadline - TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(2.9));
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

    private static HttpResponseMessage Answer(string status, string balance, TransactionId transactionId) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"status":"{{status}}","balance":"{{balance}}","currency":"BRL","transactionId":"{{transactionId.Value}}"}""",
                Encoding.UTF8,
                "application/json"),
        };

    private SeamlessOperatorWallet Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var client = new HttpClient(new ScriptedHandler(respond));
        _clients.Add(client);
        var settings = new FixedOperatorWalletSettings().UseSeamless(Casino, WalletUrl, _key);
        return new SeamlessOperatorWallet(
            client, settings, new InMemoryWalletLedger(), TimeProvider.System, _loggerFactory.CreateLogger<SeamlessOperatorWallet>());
    }

    /// <summary>The HTTP boundary of the operator, answering as the test scripts it.</summary>
    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
