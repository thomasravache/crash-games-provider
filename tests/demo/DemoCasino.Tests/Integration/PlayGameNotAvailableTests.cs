using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0045 IT-03 (§5 Disponibilidade, §6.1 "Jogo indisponível"): with the Pipa disabled for the demo operator in the real
// Apogeu, the launch is refused there (400 GAME_NOT_AVAILABLE, SPEC-0020) and the casino passes the code on as
// 404 GAME_NOT_AVAILABLE, so the page goes back to the lobby with a notice.
[Collection(ApogeuDependencies.Name)]
public sealed class PlayGameNotAvailableTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dada a Pipa desabilitada para o operador demo, quando o jogador tenta jogar, então 404 GAME_NOT_AVAILABLE")]
    [Trait("Spec", "SPEC-0045:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenPipaDisabledForDemoOperatorWhenPlayerPlaysThenGameNotAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await using var casino = LobbyCasino.Against(apogeu, LobbyCasino.NewClock());
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);
        await apogeu.WithCatalogStoreAsync(store =>
            store.SetGameEnabledAsync(new("demo"), new("pipa"), enabled: false, ct));

        using var response = await CasinoApi.PlayAsync(browser, "pipa", ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.NotFound, "GAME_NOT_AVAILABLE", ct);
    }
}
