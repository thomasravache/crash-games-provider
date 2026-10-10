using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>The player REST API of the wallet (SPEC-0021 §6) and the reading of its bodies.</summary>
internal static class WalletApi
{
    public const string BalancePath = "/api/v1/wallet/balance";
    public const string RechargePath = "/api/v1/wallet/demo/recharge";

    public static HttpRequestMessage GetBalance() => new(HttpMethod.Get, new Uri(BalancePath, UriKind.Relative));

    public static HttpRequestMessage PostRecharge() => new(HttpMethod.Post, new Uri(RechargePath, UriKind.Relative));

    /// <summary>Sends the request and returns status and body text.</summary>
    public static async Task<(HttpStatusCode Status, string Body, string? MediaType)> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        using (request)
        {
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return (response.StatusCode, body, response.Content.Headers.ContentType?.MediaType);
        }
    }

    /// <summary>Asserts a 200 body <c>{ balance: Money, currency, ... }</c> with the BRL <paramref name="balance"/> and returns its root.</summary>
    public static void AssertBalanceBody(HttpStatusCode status, string body, decimal balance, string? mode)
    {
        Assert.True(status == HttpStatusCode.OK, $"Expected 200, got {(int)status}: {body}");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("balance").ValueKind);
        Assert.Equal(balance, decimal.Parse(root.GetProperty("balance").GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture));
        Assert.Equal("BRL", root.GetProperty("currency").GetString());
        if (mode is not null)
        {
            Assert.Equal(mode, root.GetProperty("mode").GetString());
        }
    }
}
