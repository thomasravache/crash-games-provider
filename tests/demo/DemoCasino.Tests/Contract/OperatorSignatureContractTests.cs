using System.Globalization;
using System.Text;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.Security.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Contract;

// SPEC-0043 CT-01: the HMAC client of the DemoCasino signs its calls to the Apogeu exactly as the public contract of
// SPEC-0009@1 (§4): headers X-Apogeu-Key, X-Apogeu-Timestamp (Unix s), X-Apogeu-Signature =
// hex(HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" + hex(SHA256(body)))). The expected value comes
// from ReferenceSignature (the independent reference of the SPEC-0009 tests), never from the production code.
//
// Test seam: the casino's client for the Apogeu is the named HttpClient "Apogeu" of IHttpClientFactory, based on
// Apogeu:BaseUrl, whose pipeline signs every request. The test swaps only its primary handler (the network) for a
// capturing one, so the signing step under test is the real one.
public sealed class OperatorSignatureContractTests
{
    private const string ApogeuClientName = "Apogeu";
    private const string KeyHeader = "X-Apogeu-Key";
    private const string TimestampHeader = "X-Apogeu-Timestamp";
    private const string SignatureHeader = "X-Apogeu-Signature";
    private const long WindowSeconds = 300;

    public static TheoryData<string, string, string> Requests => new()
    {
        { "POST", "/api/v1/operator/launch", """{"playerId":"demo-00000000-0000-0000-0000-000000000001","nickname":"ct_01","currency":"BRL","gameCode":"pipa","mode":"demo","language":"pt-BR"}""" },
        { "GET", "/api/v1/games", string.Empty },
    };

    [Theory(DisplayName = "Dado o cliente HMAC do DemoCasino, quando assina uma requisição de teste, então a assinatura confere com a referência da SPEC-0009")]
    [Trait("Spec", "SPEC-0043:CT-01")]
    [Trait("Category", "Contract")]
    [MemberData(nameof(Requests))]
    public async Task GivenDemoCasinoHmacClientWhenSigningTestRequestThenSignatureMatchesReference(string method, string path, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        var capture = new CapturingHandler();
        await using var casino = DemoCasinoHost.Start(services =>
            services.AddHttpClient(ApogeuClientName).ConfigurePrimaryHttpMessageHandler(() => capture));
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var client = casino.Services.GetRequiredService<IHttpClientFactory>().CreateClient(ApogeuClientName);
        Assert.Equal(DemoCasinoHost.ApogeuBaseUrl, client.BaseAddress);
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path.TrimStart('/'), UriKind.Relative));
        if (bodyBytes.Length > 0)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var response = await client.SendAsync(request, ct);
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var sent = Assert.Single(capture.Requests);
        Assert.Equal(path, sent.Uri.AbsolutePath);
        Assert.Equal(bodyBytes, sent.Body);
        Assert.True(sent.Headers.TryGetValue(KeyHeader, out var keyId), $"{KeyHeader} missing.");
        Assert.Equal(casino.OperatorKeyId, keyId);
        Assert.True(sent.Headers.TryGetValue(TimestampHeader, out var timestampText), $"{TimestampHeader} missing.");
        Assert.True(
            long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp),
            $"{TimestampHeader} must be Unix seconds, got '{timestampText}'.");
        Assert.InRange(timestamp, before - WindowSeconds, after + WindowSeconds);
        Assert.True(sent.Headers.TryGetValue(SignatureHeader, out var signature), $"{SignatureHeader} missing.");

        var expected = ReferenceSignature.Compute(Encoding.UTF8.GetBytes(casino.OperatorSecret), timestamp, method, path, bodyBytes);
        Assert.Equal(expected, signature, ignoreCase: true);

        // The secret itself never travels: only its signature.
        Assert.DoesNotContain(
            sent.Headers.Values,
            value => value.Contains(casino.OperatorSecret, StringComparison.OrdinalIgnoreCase));
    }
}
