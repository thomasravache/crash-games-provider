using System.Net;
using System.Text.Json;
using Apogeu.Contracts.Tests.Support;
using Apogeu.Operators.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Operators.Tests.Contract;

/// <summary>
/// SPEC-0044 CT-01 (SPEC-0020@3, Emenda v2 dela): the real <c>GET /api/v1/games</c> carries the kit art of each game —
/// <c>thumbnailUrl</c> and <c>bannerUrl</c>, strings in the OpenAPI document the Host generates — as absolute URLs
/// <c>{Operators:GameBaseUrl}/kit/{code}/thumb.png</c> and <c>/banner.png</c> (SPEC-0044 §6, Emenda v1). The game base URL
/// is a real server of the game front's public folder (<see cref="GameClientStaticServer"/>), and both URLs are fetched:
/// 200 with an image type and a PNG body.
/// </summary>
[Collection(OperatorsApiDependencies.Name)]
public sealed class KitCatalogContractTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string OpenApiPath = "/openapi/v1.json";
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact(DisplayName = "Dado o GET /api/v1/games real com os campos novos, quando validado contra o OpenAPI e as URLs são buscadas, então confere e as imagens respondem 200 com tipo de imagem")]
    [Trait("Spec", "SPEC-0044:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealListGamesWithKitFieldsWhenValidatedAndUrlsFetchedThenConformsAndImagesAreServed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var gameFront = await GameClientStaticServer.StartAsync(ct);
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, gameFront.BaseUrl, ct);
        using var client = host.CreateClient();

        OpenApiDocument openApi;
        using (var document = await client.GetAsync(new Uri(OpenApiPath, UriKind.Relative), ct))
        {
            Assert.Equal(HttpStatusCode.OK, document.StatusCode);
            openApi = OpenApiDocument.Parse(await document.Content.ReadAsStringAsync(ct));
        }

        var schema = openApi.ResponseSchema(CatalogApi.GamesPath, "get", "200");
        var item = openApi.Resolve(schema).GetProperty("items");
        Assert.Contains("string", openApi.TypeNames(openApi.Property(item, "thumbnailUrl")));
        Assert.Contains("string", openApi.TypeNames(openApi.Property(item, "bannerUrl")));

        using var request = CatalogApi.SignedListGames(host.Key, host.UnixNow());
        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 from a signed GET {CatalogApi.GamesPath}, got {(int)response.StatusCode}: {text}");
        using var body = JsonDocument.Parse(text);
        var errors = OpenApiSchemaValidator.Validate(openApi, schema, body.RootElement);
        Assert.True(errors.Count == 0, $"GET {CatalogApi.GamesPath} 200 does not validate against the OpenAPI schema:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}{Environment.NewLine}Body: {text}");

        var pipa = Assert.Single(body.RootElement.EnumerateArray(), game => string.Equals(game.GetProperty("code").GetString(), "pipa", StringComparison.Ordinal));
        var thumbnail = KitUrl(pipa, "thumbnailUrl");
        var banner = KitUrl(pipa, "bannerUrl");
        Assert.Equal(new Uri(gameFront.BaseUrl, "/kit/pipa/thumb.png"), thumbnail);
        Assert.Equal(new Uri(gameFront.BaseUrl, "/kit/pipa/banner.png"), banner);

        using var browser = new HttpClient();
        await AssertImageAsync(browser, thumbnail, ct);
        await AssertImageAsync(browser, banner, ct);
    }

    private static Uri KitUrl(JsonElement game, string property)
    {
        Assert.True(game.TryGetProperty(property, out var value), $"The catalog item has no '{property}': {game.GetRawText()}");
        var text = value.GetString();
        Assert.True(Uri.TryCreate(text, UriKind.Absolute, out var url), $"'{property}' must be an absolute URL, got '{text}'.");
        return url;
    }

    private static async Task AssertImageAsync(HttpClient browser, Uri url, CancellationToken ct)
    {
        using var response = await browser.GetAsync(url, ct).ConfigureAwait(false);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 from {url}, got {(int)response.StatusCode}.");
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        Assert.True(mediaType.StartsWith("image/", StringComparison.Ordinal), $"Expected an image type from {url}, got '{mediaType}'.");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        Assert.True(bytes.AsSpan().StartsWith(PngSignature), $"{url} must be a PNG image ({bytes.Length} bytes received).");
    }
}
