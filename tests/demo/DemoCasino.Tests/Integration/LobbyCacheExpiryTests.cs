using Apogeu.DemoCasino.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0044 IT-03 (§6.1 "Jogo desabilitado"): the admin disables the Pipa for the demo operator on the real Apogeu; once
// the 60 s cache of the lobby expires (the TimeProvider of the DemoCasino's DI, advanced by the test, §6 Emenda v1), the
// Pipa is gone from GET /casino/games.
[Collection(ApogeuDependencies.Name)]
public sealed class LobbyCacheExpiryTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dada a Pipa desabilitada para o operador demo, quando o cache de 60 s expira, então ela some da lista")]
    [Trait("Spec", "SPEC-0044:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenPipaDisabledForDemoOperatorWhenCacheExpiresThenItLeavesTheLobby()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        var clock = LobbyCasino.NewClock();
        await using var casino = LobbyCasino.Against(apogeu, clock);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        // Control: the seed enables the Pipa, and the lobby lists it (this also fills the cache).
        using (var before = await CasinoApi.GamesAsync(browser, ct))
        {
            Assert.Contains(await CasinoApi.ReadGamesAsync(before, ct), IsPipa);
        }

        await apogeu.WithCatalogStoreAsync(store =>
            store.SetGameEnabledAsync(new("demo"), new("pipa"), enabled: false, ct));
        clock.Advance(CacheLifetime + TimeSpan.FromSeconds(1));

        using var after = await CasinoApi.GamesAsync(browser, ct);
        Assert.DoesNotContain(await CasinoApi.ReadGamesAsync(after, ct), IsPipa);
    }

    private static bool IsPipa(System.Text.Json.JsonElement game) =>
        string.Equals(game.GetProperty("code").GetString(), "pipa", StringComparison.Ordinal);
}
