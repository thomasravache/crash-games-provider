using System.Net;
using System.Text.Json;
using Apogeu.Operators.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Operators.Tests.Integration;

/// <summary>IT-03: <c>GET /api/v1/games</c> on the real Host lists the games enabled for the operator and requires the HMAC signature (SPEC-0020 §5, §6).</summary>
[Collection(OperatorsApiDependencies.Name)]
public sealed class ListGamesTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dado o Host real, quando GET /api/v1/games é chamado sem e com assinatura, então 401 e 200 com [{code:\"pipa\",…}] (só jogos habilitados)")]
    [Trait("Spec", "SPEC-0020:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostWhenListingGamesThenUnsignedIsUnauthorizedAndSignedListsPipa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await LaunchCatalogRejectionTests.AddDisabledGameAsync(host, ct);
        using var client = host.CreateClient();

        using (var unsigned = CatalogApi.UnsignedListGames())
        using (var response = await client.SendAsync(unsigned, ct))
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized,
                $"Expected 401 for an unsigned GET {CatalogApi.GamesPath}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }

        using var request = CatalogApi.SignedListGames(host.Key, host.UnixNow());
        using var signed = await client.SendAsync(request, ct);
        var text = await signed.Content.ReadAsStringAsync(ct);

        Assert.True(signed.StatusCode == HttpStatusCode.OK, $"Expected 200 from a signed GET {CatalogApi.GamesPath}, got {(int)signed.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        var game = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("pipa", game.GetProperty("code").GetString());
        Assert.Equal("Pipa", game.GetProperty("name").GetString());
        Assert.Equal(["BRL"], game.GetProperty("currencies").EnumerateArray().Select(c => c.GetString()).ToArray());
    }
}
