using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0044 IT-02 (§5 Disponibilidade): with the Apogeu unavailable (a stand-in network that cannot connect, or that answers
// an error), the logged-in player's GET /casino/games is 502 PROVIDER_UNAVAILABLE, so the lobby shows "Jogos indisponíveis
// no momento".
public sealed class LobbyProviderUnavailableTests
{
    public static TheoryData<HttpStatusCode> FailingStatuses =>
    [
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.GatewayTimeout,
    ];

    [Fact(DisplayName = "Dada a Apogeu fora do ar (conexão recusada), quando o lobby é pedido, então 502 PROVIDER_UNAVAILABLE")]
    [Trait("Spec", "SPEC-0044:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenUnreachableApogeuWhenLobbyIsRequestedThenProviderUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Unreachable();
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.GamesAsync(browser, ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.BadGateway, "PROVIDER_UNAVAILABLE", ct);
        Assert.True(apogeu.Requests > 0, "The casino must have tried the Apogeu.");
    }

    [Theory(DisplayName = "Dada a Apogeu respondendo erro, quando o lobby é pedido, então 502 PROVIDER_UNAVAILABLE")]
    [Trait("Spec", "SPEC-0044:IT-02")]
    [Trait("Category", "Integration")]
    [MemberData(nameof(FailingStatuses))]
    public async Task GivenApogeuAnsweringErrorWhenLobbyIsRequestedThenProviderUnavailable(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Answering(status, """{"code":"INTERNAL_ERROR"}""");
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.GamesAsync(browser, ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.BadGateway, "PROVIDER_UNAVAILABLE", ct);
    }
}
