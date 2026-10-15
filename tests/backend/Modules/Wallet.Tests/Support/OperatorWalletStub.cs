using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// The operator wallet of the tests (SPEC-0046 §7: "operador de teste HTTP em processo com respostas programáveis"): a real
/// Kestrel server on the loopback that implements the public operator wallet API (docs/integration/operator-wallet-api.md)
/// with balances of 3.000,00 per player and currency, idempotent by transactionId (DUPLICATE with the original balance),
/// INSUFFICIENT_FUNDS and TRANSACTION_NOT_FOUND. It records every request and answer; the test can delay the answers
/// (<see cref="Delay"/>) or make it answer 500 (<see cref="FailWhen"/>). It verifies nothing: CT-01 checks the signatures.
/// </summary>
internal sealed class OperatorWalletStub : IAsyncDisposable
{
    public const decimal OpeningBalance = 3_000.00m;

    private readonly Lock _gate = new();
    private readonly List<StubCall> _calls = [];
    private readonly List<StubRequest> _received = [];
    private readonly Dictionary<string, decimal> _balances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Applied> _applied = new(StringComparer.Ordinal);
    private readonly WebApplication _app;

    private OperatorWalletStub(WebApplication app, Uri baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    /// <summary>Base URL of the wallet (<c>walletUrl</c> of the operator), on the loopback.</summary>
    public Uri BaseUrl { get; }

    /// <summary>Time the operator takes before answering (it applies the operation anyway, even if the caller gave up).</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>When it holds for a request, the operator answers 500 without applying anything.</summary>
    public Func<StubRequest, bool>? FailWhen { get; set; }

    /// <summary>Every request received, in order of arrival (before the delay).</summary>
    public IReadOnlyList<StubRequest> Received
    {
        get
        {
            lock (_gate)
            {
                return [.. _received];
            }
        }
    }

    /// <summary>Every request answered, with its answer, in order of answer.</summary>
    public IReadOnlyList<StubCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public static async Task<OperatorWalletStub> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "OperatorWalletStub" });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        OperatorWalletStub? stub = null;
        app.MapPost("/apogeu/v1/{operation}", (HttpContext context, string operation) => stub!.HandleAsync(context, operation));

        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            stub = new OperatorWalletStub(app, new Uri(address));
            return stub;
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The requests answered for <paramref name="transactionId"/>.</summary>
    public IReadOnlyList<StubCall> CallsOf(string transactionId) =>
        [.. Calls.Where(call => string.Equals(call.Request.TransactionId, transactionId, StringComparison.Ordinal))];

    /// <summary>How many times <paramref name="transactionId"/> was applied (answered OK); a repeat answers DUPLICATE and does not count.</summary>
    public int AppliedCount(string transactionId) =>
        CallsOf(transactionId).Count(call => call.StatusCode == StatusCodes.Status200OK && string.Equals(call.Status, "OK", StringComparison.Ordinal));

    /// <summary>Balance of <paramref name="playerId"/> in <paramref name="currency"/> at the operator.</summary>
    public decimal BalanceOf(string playerId, string currency)
    {
        lock (_gate)
        {
            return _balances.TryGetValue(AccountOf(playerId, currency), out var balance) ? balance : OpeningBalance;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private static string AccountOf(string playerId, string currency) => playerId + "|" + currency;

    private static decimal AmountOf(JsonElement body) =>
        decimal.Parse(body.GetProperty("amount").GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static string Format(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private async Task HandleAsync(HttpContext context, string operation)
    {
        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted).ConfigureAwait(false);
        var request = new StubRequest(
            context.Request.Method,
            context.Request.PathBase.Add(context.Request.Path).Value ?? string.Empty,
            context.Request.ContentType,
            context.Request.Headers[SeamlessExpectations.KeyHeader].FirstOrDefault(),
            context.Request.Headers[SeamlessExpectations.TimestampHeader].FirstOrDefault(),
            context.Request.Headers[SeamlessExpectations.SignatureHeader].FirstOrDefault(),
            buffer.ToArray(),
            DateTimeOffset.UtcNow);
        lock (_gate)
        {
            _received.Add(request);
        }

        if (Delay > TimeSpan.Zero)
        {
            // The operator keeps processing even when the caller gave up waiting.
            await Task.Delay(Delay, CancellationToken.None).ConfigureAwait(false);
        }

        if (FailWhen is { } fail && fail(request))
        {
            Record(new StubCall(request, StatusCodes.Status500InternalServerError, Status: null));
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        var (status, balance, currency) = Decide(operation, request.Json);
        Record(new StubCall(request, StatusCodes.Status200OK, status));

        try
        {
            await context.Response.WriteAsJsonAsync(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["status"] = status,
                    ["balance"] = Format(balance),
                    ["currency"] = currency,
                    ["transactionId"] = request.TransactionId,
                },
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or IOException)
        {
            // The caller gave up (deadline): the operation stays applied, as at a real operator.
        }
    }

    private void Record(StubCall call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }

    private (string Status, decimal Balance, string Currency) Decide(string operation, JsonElement body)
    {
        var playerId = body.GetProperty("playerId").GetString()!;
        var currency = body.GetProperty("currency").GetString()!;
        var account = AccountOf(playerId, currency);

        lock (_gate)
        {
            var balance = _balances.TryGetValue(account, out var known) ? known : OpeningBalance;
            if (string.Equals(operation, "balance", StringComparison.Ordinal))
            {
                return ("OK", balance, currency);
            }

            var transactionId = body.GetProperty("transactionId").GetString()!;
            if (_applied.TryGetValue(transactionId, out var original))
            {
                return ("DUPLICATE", original.BalanceAfter, currency);
            }

            decimal delta;
            switch (operation)
            {
                case "debit":
                    var amount = AmountOf(body);
                    if (amount > balance)
                    {
                        return ("INSUFFICIENT_FUNDS", balance, currency);
                    }

                    delta = -amount;
                    break;
                case "credit":
                    delta = AmountOf(body);
                    break;
                case "rollback":
                    var originalId = body.GetProperty("originalTransactionId").GetString()!;
                    if (!_applied.TryGetValue(originalId, out var debit) || !string.Equals(debit.Operation, "debit", StringComparison.Ordinal))
                    {
                        return ("TRANSACTION_NOT_FOUND", balance, currency);
                    }

                    delta = -debit.Delta;
                    break;
                default:
                    return ("TRANSACTION_NOT_FOUND", balance, currency);
            }

            var after = balance + delta;
            _balances[account] = after;
            _applied[transactionId] = new Applied(operation, delta, after);
            return ("OK", after, currency);
        }
    }

    private sealed record Applied(string Operation, decimal Delta, decimal BalanceAfter);
}
