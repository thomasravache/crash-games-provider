using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0045 IT-04 (§6.1 "Apogeu indisponível", Emenda v1): with the Apogeu unavailable (a stand-in network that cannot
// connect, or that answers 500, 503 or 504), the logged-in player's POST /casino/play/pipa is 502 PROVIDER_UNAVAILABLE,
// so the game page shows the notice and goes back to the lobby. Same pattern as SPEC-0044 IT-02.
public sealed class PlayProviderUnavailableTests
{
    public static TheoryData<HttpStatusCode> FailingStatuses =>
    [
        HttpStatusCode.InternalServerError,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    [Fact(DisplayName = "Dada a Apogeu fora do ar (conexão recusada), quando o jogador tenta jogar, então 502 PROVIDER_UNAVAILABLE")]
    [Trait("Spec", "SPEC-0045:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenUnreachableApogeuWhenPlayerPlaysThenProviderUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Unreachable();
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.PlayAsync(browser, "pipa", ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.BadGateway, "PROVIDER_UNAVAILABLE", ct);
        Assert.True(apogeu.Requests > 0, "The casino must have tried the Apogeu.");
    }

    [Theory(DisplayName = "Dada a Apogeu respondendo erro, quando o jogador tenta jogar, então 502 PROVIDER_UNAVAILABLE")]
    [Trait("Spec", "SPEC-0045:IT-04")]
    [Trait("Category", "Integration")]
    [MemberData(nameof(FailingStatuses))]
    public async Task GivenApogeuAnsweringErrorWhenPlayerPlaysThenProviderUnavailable(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = StubApogeuHandler.Answering(status, """{"code":"INTERNAL_ERROR"}""");
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.PlayAsync(browser, "pipa", ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.BadGateway, "PROVIDER_UNAVAILABLE", ct);
        Assert.True(apogeu.Requests > 0, "The casino must have tried the Apogeu.");
    }
}
