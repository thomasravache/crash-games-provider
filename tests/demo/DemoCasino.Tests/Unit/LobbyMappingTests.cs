using System.Net;
using System.Text.Json;
using Apogeu.DemoCasino.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Unit;

// SPEC-0044 UT-01: the mapping of the Apogeu catalog (GET /api/v1/games, SPEC-0020@3) to the lobby (GET /casino/games),
// with the rules of §6 (Emenda v1): category = "crash" for every game; isNew = true only for the codes of
// DemoCasino:Lobby:NewGames (default ["pipa"]); code, name, thumbnailUrl and bannerUrl passed through; nothing else (the
// currencies stay on the server). Exercised through the BFF on the in-memory DemoCasino with a stand-in Apogeu that
// answers a fixed catalog (no external dependency), so the test holds whatever mapper type the implementation uses.
public sealed class LobbyMappingTests
{
    private const string NewGamesSetting = "DemoCasino:Lobby:NewGames";

    private static readonly string[] LobbyFields = ["bannerUrl", "category", "code", "isNew", "name", "thumbnailUrl"];

    private const string Catalog = """
        [
          { "code": "aviao", "name": "Avião", "currencies": ["BRL", "USD"],
            "thumbnailUrl": "https://game.apogeu.test/kit/aviao/thumb.png", "bannerUrl": "https://game.apogeu.test/kit/aviao/banner.png" },
          { "code": "pipa", "name": "Pipa", "currencies": ["BRL"],
            "thumbnailUrl": "https://game.apogeu.test/kit/pipa/thumb.png", "bannerUrl": "https://game.apogeu.test/kit/pipa/banner.png" }
        ]
        """;

    [Fact(DisplayName = "Dado o catálogo com a Pipa e outro jogo e a configuração padrão, quando mapeado para o lobby, então todos são crash e só a Pipa é Novo")]
    [Trait("Spec", "SPEC-0044:UT-01")]
    public async Task GivenCatalogAndDefaultNewGamesWhenMappedThenAllCrashAndOnlyPipaIsNew()
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Answering(HttpStatusCode.OK, Catalog);
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.GamesAsync(browser, ct);
        var games = ByCode(await CasinoApi.ReadGamesAsync(response, ct));

        Assert.Equal(["aviao", "pipa"], games.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        AssertLobbyGame(games["pipa"], "pipa", "Pipa", isNew: true);
        AssertLobbyGame(games["aviao"], "aviao", "Avião", isNew: false);
    }

    [Fact(DisplayName = "Dado DemoCasino:Lobby:NewGames = [aviao], quando o catálogo é mapeado, então só o Avião é Novo e a categoria segue crash")]
    [Trait("Spec", "SPEC-0044:UT-01")]
    public async Task GivenConfiguredNewGamesWhenMappedThenOnlyConfiguredCodesAreNew()
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Answering(HttpStatusCode.OK, Catalog);
        await using var casino = LobbyCasino.Against(
            apogeu,
            new Dictionary<string, string>(StringComparer.Ordinal) { [NewGamesSetting + ":0"] = "aviao" });
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.GamesAsync(browser, ct);
        var games = ByCode(await CasinoApi.ReadGamesAsync(response, ct));

        AssertLobbyGame(games["aviao"], "aviao", "Avião", isNew: true);
        AssertLobbyGame(games["pipa"], "pipa", "Pipa", isNew: false);
    }

    [Fact(DisplayName = "Dado um catálogo vazio, quando mapeado para o lobby, então a lista é vazia")]
    [Trait("Spec", "SPEC-0044:UT-01")]
    public async Task GivenEmptyCatalogWhenMappedThenLobbyIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Answering(HttpStatusCode.OK, "[]");
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.GamesAsync(browser, ct);

        Assert.Empty(await CasinoApi.ReadGamesAsync(response, ct));
    }

    private static Dictionary<string, JsonElement> ByCode(IReadOnlyList<JsonElement> games) =>
        games.ToDictionary(game => game.GetProperty("code").GetString()!, game => game, StringComparer.Ordinal);

    private static void AssertLobbyGame(JsonElement game, string code, string name, bool isNew)
    {
        Assert.Equal(LobbyFields, game.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(code, game.GetProperty("code").GetString());
        Assert.Equal(name, game.GetProperty("name").GetString());
        Assert.Equal("crash", game.GetProperty("category").GetString());
        Assert.Equal(isNew ? JsonValueKind.True : JsonValueKind.False, game.GetProperty("isNew").ValueKind);
        Assert.Equal($"https://game.apogeu.test/kit/{code}/thumb.png", game.GetProperty("thumbnailUrl").GetString());
        Assert.Equal($"https://game.apogeu.test/kit/{code}/banner.png", game.GetProperty("bannerUrl").GetString());
    }
}
