using System.Net;
using System.Text.Json;
using Apogeu.Contracts.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Contract;

/// <summary>
/// CT-01: the real responses of IT-01 (launch), IT-02 (launch rejections) and IT-04 (session exchange) validate against the
/// OpenAPI document the Host generates (SPEC-0006), the contract v1 consumed by SPEC-0010.
/// </summary>
[Collection(SecurityDependencies.Name)]
public sealed class SecurityContractTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string OpenApiPath = "/openapi/v1.json";

    [Fact(DisplayName = "Dado o OpenAPI gerado, quando as respostas reais de lançamento (200, 400, 401, 403) são validadas, então conferem com o contrato v1")]
    [Trait("Spec", "SPEC-0009:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedOpenApiWhenLaunchResponsesAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var openApi = await OpenApiAsync(client, ct);

        var ok = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow());
        var okSchema = await AssertConformsAsync(openApi, client, ok, OperatorApi.LaunchPath, HttpStatusCode.OK, ct);
        Assert.Contains("string", openApi.TypeNames(openApi.Property(okSchema, "launchUrl")));
        Assert.Contains("string", openApi.TypeNames(openApi.Property(okSchema, "expiresAt")));

        var cases = new (HttpRequestMessage Request, HttpStatusCode Status)[]
        {
            (OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), TestApiKey.Generate(), host.UnixNow()), HttpStatusCode.Unauthorized),
            (OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow() - 301), HttpStatusCode.Unauthorized),
            (OperatorApi.SignedLaunch(LaunchBodies.WithoutPlayerId(), host.PrimaryKey, host.UnixNow()), HttpStatusCode.BadRequest),
            (OperatorApi.SignedLaunch(LaunchBodies.RealMoney(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow()), HttpStatusCode.Forbidden),
        };
        foreach (var (request, status) in cases)
        {
            var schema = await AssertConformsAsync(openApi, client, request, OperatorApi.LaunchPath, status, ct);
            Assert.Contains("string", openApi.TypeNames(openApi.Property(schema, "code")));
        }
    }

    [Fact(DisplayName = "Dado o OpenAPI gerado, quando as respostas reais da troca por sessão (200, 401) são validadas, então conferem com o contrato v1")]
    [Trait("Spec", "SPEC-0009:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedOpenApiWhenSessionResponsesAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var openApi = await OpenApiAsync(client, ct);
        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct);

        var okSchema = await AssertConformsAsync(openApi, client, OperatorApi.CreateSession(launch.LaunchToken), OperatorApi.SessionsPath, HttpStatusCode.OK, ct);
        foreach (var property in (string[])["sessionToken", "expiresAt", "salaId", "currency", "mode"])
        {
            Assert.Contains("string", openApi.TypeNames(openApi.Property(okSchema, property)));
        }

        Assert.True(openApi.IsNullable(openApi.Property(openApi.Property(okSchema, "player"), "nickname")), "player.nickname is optional (SPEC-0009 §6).");

        var used = await AssertConformsAsync(openApi, client, OperatorApi.CreateSession(launch.LaunchToken), OperatorApi.SessionsPath, HttpStatusCode.Unauthorized, ct);
        Assert.Contains("string", openApi.TypeNames(openApi.Property(used, "code")));
    }

    private static async Task<OpenApiDocument> OpenApiAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri(OpenApiPath, UriKind.Relative), ct).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return OpenApiDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Sends the request, then validates the body against the schema of that status and media type; returns the schema.</summary>
    private static async Task<JsonElement> AssertConformsAsync(
        OpenApiDocument openApi, HttpClient client, HttpRequestMessage request, string path, HttpStatusCode status, CancellationToken ct)
    {
        using (request)
        {
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Assert.True(response.StatusCode == status, $"Expected {(int)status} from POST {path}, got {(int)response.StatusCode}: {text}");
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            Assert.True(mediaType is "application/json" or "application/problem+json", $"Expected a JSON body for {(int)status}, got '{mediaType}'.");

            var schema = SchemaOf(openApi, path, "post", ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), mediaType!);
            using var body = JsonDocument.Parse(text);
            var errors = OpenApiSchemaValidator.Validate(openApi, schema, body.RootElement);
            Assert.True(
                errors.Count == 0,
                $"POST {path} {(int)status} does not validate against the OpenAPI schema:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}{Environment.NewLine}Body: {text}");
            return schema;
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

    private Task<SecurityHost> StartAsync(CancellationToken ct) =>
        SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
}
