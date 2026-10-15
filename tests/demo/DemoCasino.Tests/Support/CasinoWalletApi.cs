using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Apogeu.Security.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// Calls of the public operator wallet API (SPEC-0021@4, docs/integration/operator-wallet-api.md) that the DemoCasino
/// implements (SPEC-0047 §6): <c>POST /apogeu/v1/{balance|debit|credit|rollback}</c>, JSON bodies exactly as the contract
/// (§4), signed as the Apogeu signs them (§3) with the independent reference formula of SPEC-0009
/// (<see cref="ReferenceSignature"/>), and the casino's own <c>GET /casino/balance</c>.
/// </summary>
/// <remarks>
/// The key the Apogeu signs with is the demo operator key the casino already holds (<c>Apogeu:OperatorKeyId</c>,
/// <c>Apogeu:OperatorSecret</c>): the contract says the wallet calls are signed "com o segredo da chave de API combinada com o
/// operador (mesmo esquema da integração operador → Apogeu)".
/// </remarks>
internal static class CasinoWalletApi
{
    public const string BalancePath = "/apogeu/v1/balance";
    public const string DebitPath = "/apogeu/v1/debit";
    public const string CreditPath = "/apogeu/v1/credit";
    public const string RollbackPath = "/apogeu/v1/rollback";

    /// <summary>The casino's own balance of the session player (SPEC-0047 §6): <c>GET /casino/balance → { balance, currency }</c>.</summary>
    public const string CasinoBalancePath = "/casino/balance";

    public const string CurrencyCode = "BRL";

    public const decimal OpeningBalance = 3_000.00m;

    public const string KeyHeader = "X-Apogeu-Key";
    public const string TimestampHeader = "X-Apogeu-Timestamp";
    public const string SignatureHeader = "X-Apogeu-Signature";

    public static string DebitId(Guid betId) => "bet:" + betId.ToString("D") + ":debit";

    public static string CreditId(Guid betId) => "bet:" + betId.ToString("D") + ":credit";

    public static string RollbackId(Guid betId) => "bet:" + betId.ToString("D") + ":rollback";

    /// <summary>The body of <c>balance</c>: only <c>playerId</c> and <c>currency</c> (§4).</summary>
    public static WalletCall Balance(string playerId) => new(BalancePath, Json(writer =>
    {
        writer.WriteString("playerId", playerId);
        writer.WriteString("currency", CurrencyCode);
    }), TransactionId: null);

    /// <summary>A debit of <paramref name="amount"/> for <paramref name="betId"/>; <paramref name="queued"/> sends <c>roundId: null</c> (§4).</summary>
    public static WalletCall Debit(string playerId, decimal amount, Guid betId, bool queued = false) =>
        Operation(DebitPath, DebitId(betId), playerId, amount, betId, queued ? null : Guid.NewGuid());

    public static WalletCall Credit(string playerId, decimal amount, Guid betId) =>
        Operation(CreditPath, CreditId(betId), playerId, amount, betId, Guid.NewGuid());

    /// <summary>The rollback of the debit of <paramref name="betId"/> (no amount: it gives back exactly the debit, §4).</summary>
    public static WalletCall Rollback(string playerId, Guid betId)
    {
        var transactionId = RollbackId(betId);
        return new WalletCall(RollbackPath, Json(writer =>
        {
            writer.WriteString("transactionId", transactionId);
            writer.WriteString("originalTransactionId", DebitId(betId));
            writer.WriteString("playerId", playerId);
            writer.WriteString("currency", CurrencyCode);
            writer.WriteString("roundId", Guid.NewGuid().ToString("D"));
            writer.WriteString("betId", betId.ToString("D"));
        }), transactionId);
    }

    /// <summary>Sends <paramref name="call"/> signed with the key (§3), at <paramref name="timestamp"/> (now by default).</summary>
    public static Task<HttpResponseMessage> SendSignedAsync(
        HttpClient client, WalletCall call, string keyId, string secret, CancellationToken ct, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(call);
        var unix = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var signature = ReferenceSignature.Compute(Encoding.UTF8.GetBytes(secret), unix, "POST", call.Path, call.Body);
        return SendAsync(client, call, keyId, unix.ToString(CultureInfo.InvariantCulture), signature, ct);
    }

    /// <summary>Sends <paramref name="call"/> with the given headers (any of them null is left out).</summary>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, WalletCall call, string? keyId, string? timestamp, string? signature, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(call);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(call.Path, UriKind.Relative));
        request.Content = new ByteArrayContent(call.Body);

        // As the Apogeu sends it (SeamlessOperatorWallet): application/json; charset=utf-8.
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        if (keyId is not null)
        {
            request.Headers.TryAddWithoutValidation(KeyHeader, keyId);
        }

        if (timestamp is not null)
        {
            request.Headers.TryAddWithoutValidation(TimestampHeader, timestamp);
        }

        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(SignatureHeader, signature);
        }

        return await client.SendAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>Signed call and its answer, asserted to be a 200 JSON answer of the contract (§5).</summary>
    public static async Task<WalletAnswer> CallAsync(HttpClient client, WalletCall call, string keyId, string secret, CancellationToken ct)
    {
        using var response = await SendSignedAsync(client, call, keyId, secret, ct).ConfigureAwait(false);
        return await ReadAnswerAsync(response, call, ct).ConfigureAwait(false);
    }

    /// <summary>Reads a wallet answer: 200, JSON object with <c>status</c>, <c>balance</c>, <c>currency</c> and <c>transactionId</c>.</summary>
    public static async Task<WalletAnswer> ReadAnswerAsync(HttpResponseMessage response, WalletCall call, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(call);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 from POST {call.Path}, got {(int)response.StatusCode}. Body: {text}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        return new WalletAnswer(
            StringOf(root, "status"),
            StringOf(root, "balance"),
            StringOf(root, "currency"),
            StringOf(root, "transactionId"),
            root.Clone());
    }

    /// <summary>
    /// <c>GET /casino/balance</c> of the session player: 200 <c>{ balance, currency }</c>; the balance read as a decimal
    /// (a decimal string as in the wallet contract, or a JSON number).
    /// </summary>
    public static async Task<(decimal Balance, string? Currency)> CasinoBalanceAsync(HttpClient browser, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(browser);
        using var response = await browser.GetAsync(new Uri(CasinoBalancePath, UriKind.Relative), ct).ConfigureAwait(false);
        var body = await CasinoApi.ReadOkJsonAsync(response, ct).ConfigureAwait(false);
        Assert.True(body.TryGetProperty("balance", out var balance), $"GET {CasinoBalancePath} must carry balance: {body.GetRawText()}");
        Assert.True(
            balance.ValueKind is JsonValueKind.String or JsonValueKind.Number,
            $"GET {CasinoBalancePath}: balance must be a decimal: {body.GetRawText()}");
        var value = balance.ValueKind == JsonValueKind.Number
            ? balance.GetDecimal()
            : decimal.Parse(balance.GetString()!, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return (value, StringOf(body, "currency"));
    }

    /// <summary>The <c>playerId</c> of a fresh casino session on <paramref name="browser"/> (SPEC-0043 §6).</summary>
    public static async Task<string> EnterAsync(HttpClient browser, CancellationToken ct)
    {
        using var entered = await CasinoApi.EnterAsync(browser, CasinoApi.NewNickname(), CurrencyCode, ct).ConfigureAwait(false);
        return (await CasinoApi.ReadOkJsonAsync(entered, ct).ConfigureAwait(false)).GetProperty("playerId").GetString()!;
    }

    /// <summary>The decimal of a contract amount string (<c>"2990.00"</c>).</summary>
    public static decimal Amount(string? value)
    {
        Assert.NotNull(value);
        return decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    private static WalletCall Operation(string path, string transactionId, string playerId, decimal amount, Guid betId, Guid? roundId) =>
        new(path, Json(writer =>
        {
            writer.WriteString("transactionId", transactionId);
            writer.WriteString("playerId", playerId);
            writer.WriteString("currency", CurrencyCode);
            writer.WriteString("amount", amount.ToString("F2", CultureInfo.InvariantCulture));
            if (roundId is { } round)
            {
                writer.WriteString("roundId", round.ToString("D"));
            }
            else
            {
                writer.WriteNull("roundId");
            }

            writer.WriteString("betId", betId.ToString("D"));
        }), transactionId);

    private static string? StringOf(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static byte[] Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}
