using System.Net;
using System.Text;
using Apogeu.Operators.Contracts;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Logging;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure;
using Apogeu.Wallet.Infrastructure.Seamless;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Unit;

/// <summary>
/// SPEC-0065 UT-02 (Emenda v2): plain http to an operator wallet is accepted only for the hosts listed in
/// <c>Wallet:Operators:{op}:AllowHttpHosts</c> (the DemoCasino on the internal network of the production compose); any other
/// host over http stays a misconfiguration (the loopback rule of SPEC-0046 is unchanged). The settings are read from the
/// configuration as in production (the settings the module registers).
/// </summary>
public sealed class SeamlessAllowHttpHostsTests : IDisposable
{
    private const string ListedHost = "demo-casino";

    private readonly TestApiKey _key = TestApiKey.Generate();
    private readonly LogCapture _logs = new();
    private readonly LoggerFactory _loggerFactory;
    private readonly List<HttpClient> _clients = [];
    private readonly List<ServiceProvider> _providers = [];

    public SeamlessAllowHttpHostsTests() => _loggerFactory = new LoggerFactory([_logs]);

    [Fact(DisplayName = "Dada a carteira seamless com AllowHttpHosts=[demo-casino], quando a Apogeu debita em http://demo-casino:8080, então a chamada é feita e Ok volta")]
    [Trait("Spec", "SPEC-0065:UT-02")]
    public async Task GivenListedHttpHostWhenDebitingThenTheCallIsMadeAndOkReturns()
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var calls = 0;
        var wallet = Create(new Uri($"http://{ListedHost}:8080"), (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Answer("2990.00", debit.TransactionId.Value));
        });

        var result = await wallet.DebitAsync(debit, ct);

        Assert.True(calls == 1, $"Plain http to the listed host '{ListedHost}' was refused ({calls} call(s) made, result {result}).");
        AssertOk(result, 2_990.00m);
    }

    [Fact(DisplayName = "Dada a carteira seamless com AllowHttpHosts=[demo-casino], quando a Apogeu debita em http para um host não listado, então Unavailable(retryable: false), log Error e nenhuma chamada sai")]
    [Trait("Spec", "SPEC-0065:UT-02")]
    public async Task GivenUnlistedHttpHostWhenDebitingThenRefusedAsMisconfiguration()
    {
        var ct = TestContext.Current.CancellationToken;
        var debit = Debit(NewCasinoPlayer(), 10.00m, Guid.NewGuid());
        var calls = 0;
        var wallet = Create(new Uri("http://other-casino:8080"), (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Answer("2990.00", debit.TransactionId.Value));
        });

        var result = await wallet.DebitAsync(debit, ct);

        Assert.Equal(new WalletUnavailable(Retryable: false), result);
        Assert.Equal(0, calls);
        Assert.Contains(_logs.Entries, entry => entry.Level >= LogLevel.Error);
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        _loggerFactory.Dispose();
        _logs.Dispose();
    }

    private static HttpResponseMessage Answer(string balance, string transactionId) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"status":"OK","balance":"{{balance}}","currency":"BRL","transactionId":"{{transactionId}}"}""",
                Encoding.UTF8,
                "application/json"),
        };

    /// <summary>The seamless wallet with the operator configured as the production compose does (§6).</summary>
    private SeamlessOperatorWallet Create(Uri walletUrl, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var section = $"Wallet:Operators:{Casino.Value}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [$"{section}:Mode"] = "Seamless",
                [$"{section}:WalletUrl"] = walletUrl.ToString(),
                [$"{section}:KeyId"] = _key.Id,
                [$"{section}:Secret"] = _key.Secret,
                [$"{section}:AllowHttpHosts:0"] = ListedHost,
            })
            .Build();
        var client = new HttpClient(new ScriptedHandler(respond));
        _clients.Add(client);
        return new SeamlessOperatorWallet(
            client,
            ProductionSettings(configuration),
            new InMemoryWalletLedger(),
            TimeProvider.System,
            _loggerFactory.CreateLogger<SeamlessOperatorWallet>());
    }

    /// <summary>
    /// The <see cref="IOperatorWalletSettings"/> the module registers on the application configuration (nothing else of the
    /// module is resolved, so no database is touched).
    /// </summary>
    private IOperatorWalletSettings ProductionSettings(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddWalletInfrastructure(_ => throw new InvalidOperationException("The wallet settings do not use the database."));
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<IOperatorWalletSettings>();
    }

    /// <summary>The HTTP boundary of the operator, answering as the test scripts it.</summary>
    private sealed class ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
