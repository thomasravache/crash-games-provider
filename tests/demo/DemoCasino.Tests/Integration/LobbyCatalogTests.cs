using System.Net;
using System.Text.Json;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0044 IT-01 (§5 Segurança, §6): the real DemoCasino against the real Apogeu (the Host in memory on real PostgreSQL
// and Redis, with the SPEC-0020 seed). The logged-in player asks GET /casino/games and gets the Pipa with its kit art,
// exactly as the signed catalog of the Apogeu lists it; without a session, 401 and the Apogeu is never needed. The
// operator credentials never reach the browser.
[Collection(ApogeuDependencies.Name)]
public sealed class LobbyCatalogTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dados DemoCasino e Apogeu reais, quando o jogador logado pede /casino/games, então recebe a Pipa com miniatura e banner do catálogo")]
    [Trait("Spec", "SPEC-0044:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealCasinoAndApogeuWhenLoggedPlayerAsksGamesThenPipaWithKitArt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await using var casino = LobbyCasino.Against(apogeu, LobbyCasino.NewClock());
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.GamesAsync(browser, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        var games = await CasinoApi.ReadGamesAsync(response, ct);

        var pipa = Assert.Single(games, game => string.Equals(game.GetProperty("code").GetString(), "pipa", StringComparison.Ordinal));
        Assert.Equal("Pipa", pipa.GetProperty("name").GetString());
        Assert.Equal("crash", pipa.GetProperty("category").GetString());
        Assert.Equal(JsonValueKind.True, pipa.GetProperty("isNew").ValueKind);
        Assert.Equal(new Uri(ApogeuApiHost.GameBaseUrl, "/kit/pipa/thumb.png").ToString(), pipa.GetProperty("thumbnailUrl").GetString());
        Assert.Equal(new Uri(ApogeuApiHost.GameBaseUrl, "/kit/pipa/banner.png").ToString(), pipa.GetProperty("bannerUrl").GetString());

        // The lobby shows exactly what the operator has enabled: the same games and art as the Apogeu catalog.
        var catalog = await apogeu.ListGamesAsync(ct);
        Assert.Equal(
            catalog.EnumerateArray().Select(Art).Order(StringComparer.Ordinal),
            games.Select(Art).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // §5: the call is signed on the server; nothing of the operator key reaches the browser.
        var everything = text + "\n" + string.Join("\n", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        Assert.DoesNotContain(casino.OperatorSecret, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(casino.OperatorKeyId, everything, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Dados DemoCasino e Apogeu reais, quando /casino/games é pedido sem sessão, então 401")]
    [Trait("Spec", "SPEC-0044:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealCasinoAndApogeuWhenGamesAskedWithoutSessionThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await using var casino = LobbyCasino.Against(apogeu, LobbyCasino.NewClock());
        using var anonymous = casino.CreateBrowserClient();

        using var response = await CasinoApi.GamesAsync(anonymous, ct);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 from GET {CasinoApi.GamesPath} without a session, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }

    private static string Art(JsonElement game) =>
        string.Join(
            "|",
            game.GetProperty("code").GetString(),
            game.GetProperty("name").GetString(),
            game.GetProperty("thumbnailUrl").GetString(),
            game.GetProperty("bannerUrl").GetString());
}
