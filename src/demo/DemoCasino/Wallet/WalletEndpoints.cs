using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apogeu.DemoCasino.Session;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apogeu.DemoCasino.Wallet;

/// <summary>
/// The casino wallet (SPEC-0047 §6). For the Apogeu, the public wallet contract (SPEC-0021@4,
/// docs/integration/operator-wallet-api.md): <c>POST /apogeu/v1/{balance|debit|credit|rollback}</c>, signed by the Apogeu
/// (anything else is 401 and nothing is processed), always 200 <c>{ status, balance, currency, transactionId }</c> with a
/// definitive result. For the player of the casino session, <c>GET /casino/balance → { balance, currency }</c> (401 without
/// a session). Calls are counted by operation and status (SPEC-0047 §8).
/// </summary>
internal static class WalletEndpoints
{
    public const string InvalidRequest = "INVALID_REQUEST";

    /// <summary>Largest wallet request body read (the contract bodies are a few hundred bytes).</summary>
    private const int MaxBodyBytes = 16 * 1024;

    private const int AmountScale = 2;

    public static IServiceCollection AddCasinoWallet(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.AddSingleton<CasinoWallet>();
        services.AddSingleton<ApogeuCallVerifier>();
        services.AddSingleton<WalletMetrics>();
        return services;
    }

    public static IEndpointRouteBuilder MapCasinoWallet(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/apogeu/v1");
        api.MapPost("/balance", (HttpContext context, CasinoWallet wallet, ApogeuCallVerifier verifier, WalletMetrics metrics) =>
            HandleAsync(Operation.Balance, context, wallet, verifier, metrics));
        api.MapPost("/debit", (HttpContext context, CasinoWallet wallet, ApogeuCallVerifier verifier, WalletMetrics metrics) =>
            HandleAsync(Operation.Debit, context, wallet, verifier, metrics));
        api.MapPost("/credit", (HttpContext context, CasinoWallet wallet, ApogeuCallVerifier verifier, WalletMetrics metrics) =>
            HandleAsync(Operation.Credit, context, wallet, verifier, metrics));
        api.MapPost("/rollback", (HttpContext context, CasinoWallet wallet, ApogeuCallVerifier verifier, WalletMetrics metrics) =>
            HandleAsync(Operation.Rollback, context, wallet, verifier, metrics));

        endpoints.MapGroup("/casino").MapGet("/balance", CasinoBalance);
        return endpoints;
    }

    private static IResult CasinoBalance(HttpContext context, CasinoSessionCookie cookie, CasinoWallet wallet)
    {
        if (cookie.Read(context) is not { } player)
        {
            return Results.Unauthorized();
        }

        var outcome = wallet.Balance(player.PlayerId, player.Currency);
        return Results.Json(new CasinoBalanceResponse(Amount(outcome.Balance), player.Currency));
    }

    private static async Task<IResult> HandleAsync(
        Operation operation, HttpContext context, CasinoWallet wallet, ApogeuCallVerifier verifier, WalletMetrics metrics)
    {
        var body = await ReadBodyAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        if (body is null || !verifier.IsValid(context.Request, body))
        {
            metrics.Call(operation, "unauthorized");
            return Results.Unauthorized();
        }

        var call = Parse(operation, body);
        if (call is null)
        {
            metrics.Call(operation, "invalid");
            return Results.Json(new ErrorResponse(InvalidRequest), statusCode: StatusCodes.Status400BadRequest);
        }

        var outcome = operation switch
        {
            Operation.Balance => wallet.Balance(call.PlayerId, call.Currency),
            Operation.Debit => wallet.Debit(call.TransactionId!, call.PlayerId, call.Currency, call.Amount),
            Operation.Credit => wallet.Credit(call.TransactionId!, call.PlayerId, call.Currency, call.Amount),
            Operation.Rollback => wallet.Rollback(call.TransactionId!, call.OriginalTransactionId!, call.PlayerId, call.Currency),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown wallet operation."),
        };
        metrics.Call(operation, outcome.Status);
        return Results.Json(new WalletResponse(outcome.Status, Amount(outcome.Balance), call.Currency, call.TransactionId));
    }

    /// <summary>The exact body bytes (what the signature covers), or null when it is larger than the limit.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>The fields of the contract request (§4) each operation needs, or null when one is missing or invalid.</summary>
    private static WalletCall? Parse(Operation operation, byte[] body)
    {
        WalletRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<WalletRequest>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is not { PlayerId: { Length: > 0 } playerId, Currency: { Length: > 0 } currency })
        {
            return null;
        }

        if (operation == Operation.Balance)
        {
            return new WalletCall(TransactionId: null, OriginalTransactionId: null, playerId, currency, Amount: 0m);
        }

        if (string.IsNullOrEmpty(request.TransactionId))
        {
            return null;
        }

        if (operation == Operation.Rollback)
        {
            return string.IsNullOrEmpty(request.OriginalTransactionId)
                ? null
                : new WalletCall(request.TransactionId, request.OriginalTransactionId, playerId, currency, Amount: 0m);
        }

        return decimal.TryParse(request.Amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            && amount > 0m && decimal.Round(amount, AmountScale) == amount
            ? new WalletCall(request.TransactionId, OriginalTransactionId: null, playerId, currency, amount)
            : null;
    }

    private static string Amount(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    internal enum Operation
    {
        Balance,
        Debit,
        Credit,
        Rollback,
    }

    /// <summary>The body of a wallet call (contract §4); <c>roundId</c> and <c>betId</c> are not needed by the casino.</summary>
    internal sealed record WalletRequest(string? TransactionId, string? OriginalTransactionId, string? PlayerId, string? Currency, string? Amount);

    private sealed record WalletCall(string? TransactionId, string? OriginalTransactionId, string PlayerId, string Currency, decimal Amount);

    internal sealed record WalletResponse(
        string Status,
        string Balance,
        string Currency,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TransactionId);

    internal sealed record CasinoBalanceResponse(string Balance, string Currency);

    internal sealed record ErrorResponse(string Code);

    /// <summary>Wallet calls by operation and status (SPEC-0047 §8: "contagem de chamadas por status").</summary>
    internal sealed class WalletMetrics : IDisposable
    {
        private readonly Meter _meter;
        private readonly Counter<long> _calls;

        public WalletMetrics(IMeterFactory meters)
        {
            ArgumentNullException.ThrowIfNull(meters);
            _meter = meters.Create("Apogeu.DemoCasino.Wallet");
            _calls = _meter.CreateCounter<long>(
                "democasino.wallet.calls",
                unit: "{call}",
                description: "Calls of the Apogeu to the casino wallet, by operation and status (SPEC-0047 §8).");
        }

        public void Call(Operation operation, string status) =>
            _calls.Add(
                1,
                new KeyValuePair<string, object?>("operation", operation.ToString().ToLowerInvariant()),
                new KeyValuePair<string, object?>("status", status));

        public void Dispose() => _meter.Dispose();
    }
}
