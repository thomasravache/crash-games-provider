using System.Globalization;
using System.Net;
using System.Text.Json;
using Apogeu.Contracts.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Contract;

/// <summary>
/// CT-01: the real responses of IT-04 (<c>GET /api/v1/wallet/balance</c> 200, <c>POST /api/v1/wallet/demo/recharge</c> 200
/// and 409) validate against the OpenAPI document the Host generates (SPEC-0006), the contract v2 consumed by SPEC-0022,
/// SPEC-0023, SPEC-0024, SPEC-0025 and SPEC-0026.
/// </summary>
[Collection(WalletApiDependencies.Name)]
public sealed class WalletContractTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string OpenApiPath = "/openapi/v1.json";

    [Fact(DisplayName = "Dado o OpenAPI gerado, quando as respostas reais de saldo (200) e recarga (200, 409) são validadas, então conferem com o contrato v2")]
    [Trait("Spec", "SPEC-0021:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedOpenApiWhenWalletResponsesAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await WalletApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        var low = NewPlayer();
        var full = NewPlayer();
        AssertOk(await host.WithWalletAsync(wallet => wallet.DebitAsync(Debit(low, 2_999.50m, Guid.NewGuid()), ct)), 0.50m);
        using var lowClient = host.CreateClient(low.PlayerId.Value);
        using var fullClient = host.CreateClient(full.PlayerId.Value);
        using var anonymous = host.CreateAnonymousClient();
        var openApi = await OpenApiAsync(anonymous, ct);

        var (balanceSchema, _) = await AssertConformsAsync(openApi, lowClient, WalletApi.GetBalance(), WalletApi.BalancePath, "get", HttpStatusCode.OK, ct);
        Assert.Equal("money", FormatOf(openApi, balanceSchema, "balance"));
        Assert.Contains("string", openApi.TypeNames(openApi.Property(balanceSchema, "currency")));
        Assert.Contains("string", openApi.TypeNames(openApi.Property(balanceSchema, "mode")));

        var (rechargeSchema, _) = await AssertConformsAsync(openApi, lowClient, WalletApi.PostRecharge(), WalletApi.RechargePath, "post", HttpStatusCode.OK, ct);
        Assert.Equal("money", FormatOf(openApi, rechargeSchema, "balance"));

        var (conflictSchema, conflictBody) = await AssertConformsAsync(openApi, fullClient, WalletApi.PostRecharge(), WalletApi.RechargePath, "post", HttpStatusCode.Conflict, ct);
        Assert.Contains("string", openApi.TypeNames(openApi.Property(conflictSchema, "code")));
        Assert.Equal("RECHARGE_NOT_ALLOWED", ErrorBodies.CodeOf(conflictBody));
    }

    /// <summary>The <c>format</c> of a property (after <c>$ref</c>): <c>money</c> for the decimal-string amounts of ADR-0008.</summary>
    private static string? FormatOf(OpenApiDocument openApi, JsonElement objectSchema, string name)
    {
        var property = openApi.Resolve(openApi.Property(objectSchema, name));
        return property.ValueKind == JsonValueKind.Object && property.TryGetProperty("format", out var format) ? format.GetString() : null;
    }

    private static async Task<OpenApiDocument> OpenApiAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri(OpenApiPath, UriKind.Relative), ct).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return OpenApiDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Sends the request, then validates the body against the schema of that status and media type.</summary>
    private static async Task<(JsonElement Schema, string Body)> AssertConformsAsync(
        OpenApiDocument openApi, HttpClient client, HttpRequestMessage request, string path, string method, HttpStatusCode status, CancellationToken ct)
    {
        using (request)
        {
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Assert.True(response.StatusCode == status, $"Expected {(int)status} from {method.ToUpperInvariant()} {path}, got {(int)response.StatusCode}: {text}");
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            Assert.True(mediaType is "application/json" or "application/problem+json", $"Expected a JSON body for {(int)status}, got '{mediaType}'.");

            var schema = SchemaOf(openApi, path, method, ((int)status).ToString(CultureInfo.InvariantCulture), mediaType!);
            using var body = JsonDocument.Parse(text);
            var errors = OpenApiSchemaValidator.Validate(openApi, schema, body.RootElement);
            Assert.True(
                errors.Count == 0,
                $"{method.ToUpperInvariant()} {path} {(int)status} does not validate against the OpenAPI schema:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}{Environment.NewLine}Body: {text}");
            return (schema, text);
        }
    }

    private static JsonElement SchemaOf(OpenApiDocument openApi, string path, string method, string status, string mediaType)
    {
        if (openApi.Root.TryGetProperty("paths", out var paths)
            && paths.TryGetProperty(path, out var item)
            && item.TryGetProperty(method, out var operation)
            && operation.TryGetProperty("responses", out var responses)
            && responses.TryGetProperty(status, out var response)
            && response.TryGetProperty("content", out var content)
            && content.TryGetProperty(mediaType, out var media)
            && media.TryGetProperty("schema", out var schema))
        {
            return schema;
        }

        Assert.Fail($"The OpenAPI document has no '{status}' {mediaType} response schema for {method.ToUpperInvariant()} {path}.");
        return default;
    }
}
