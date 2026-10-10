using System.Globalization;
using System.Net;
using System.Text.Json;
using Apogeu.Contracts.Tests.Support;
using Apogeu.Operators.Tests.Integration;
using Apogeu.Operators.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Operators.Tests.Contract;

/// <summary>
/// CT-01: the real responses of IT-02 (launch rejections) and IT-03 (<c>GET /api/v1/games</c>) validate against the OpenAPI
/// document the Host generates (SPEC-0006), the contract v1 consumed by SPEC-0022 and SPEC-0026.
/// </summary>
[Collection(OperatorsApiDependencies.Name)]
public sealed class CatalogContractTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string OpenApiPath = "/openapi/v1.json";

    [Fact(DisplayName = "Dado o OpenAPI gerado, quando as recusas reais do lançamento (GAME_NOT_AVAILABLE, CURRENCY_NOT_ENABLED) são validadas, então conferem com o contrato v1")]
    [Trait("Spec", "SPEC-0020:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedOpenApiWhenLaunchRejectionsAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await LaunchCatalogRejectionTests.AddDisabledGameAsync(host, ct);
        using var client = host.CreateClient();
        var openApi = await OpenApiAsync(client, ct);

        var cases = new (byte[] Body, string Code)[]
        {
            (CatalogApi.LaunchBody("aviao", "BRL"), "GAME_NOT_AVAILABLE"),
            (CatalogApi.LaunchBody(LaunchCatalogRejectionTests.DisabledGame.Code.Value, "BRL"), "GAME_NOT_AVAILABLE"),
            (CatalogApi.LaunchBody("pipa", "USD"), "CURRENCY_NOT_ENABLED"),
        };
        foreach (var (body, code) in cases)
        {
            var request = CatalogApi.SignedLaunch(body, host.Key, host.UnixNow());
            var (schema, text) = await AssertConformsAsync(openApi, client, request, CatalogApi.LaunchPath, "post", HttpStatusCode.BadRequest, ct);
            Assert.Contains("string", openApi.TypeNames(openApi.Property(schema, "code")));
            Assert.Equal(code, ErrorBodies.CodeOf(text));
        }
    }

    [Fact(DisplayName = "Dado o OpenAPI gerado, quando as respostas reais de GET /api/v1/games (200, 401) são validadas, então conferem com o contrato v1")]
    [Trait("Spec", "SPEC-0020:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedOpenApiWhenListGamesResponsesAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        using var client = host.CreateClient();
        var openApi = await OpenApiAsync(client, ct);

        var (okSchema, okText) = await AssertConformsAsync(
            openApi, client, CatalogApi.SignedListGames(host.Key, host.UnixNow()), CatalogApi.GamesPath, "get", HttpStatusCode.OK, ct);
        Assert.Contains("array", openApi.TypeNames(okSchema));
        using (var json = JsonDocument.Parse(okText))
        {
            Assert.Contains(json.RootElement.EnumerateArray(), game => string.Equals(game.GetProperty("code").GetString(), "pipa", StringComparison.Ordinal));
        }

        var (unauthorized, _) = await AssertConformsAsync(
            openApi, client, CatalogApi.UnsignedListGames(), CatalogApi.GamesPath, "get", HttpStatusCode.Unauthorized, ct);
        Assert.Contains("string", openApi.TypeNames(openApi.Property(unauthorized, "code")));
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
