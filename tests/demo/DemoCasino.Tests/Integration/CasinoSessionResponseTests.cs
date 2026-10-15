using System.Text.Json;
using Apogeu.DemoCasino.Tests.Support;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0043 IT-02 (§5 Segurança): the session response sets an HttpOnly, SameSite=Lax cookie and nothing in the
// responses carries the operator secret (ADR-0027: the HMAC secret stays on the server).
public sealed class CasinoSessionResponseTests(DemoCasinoFixture casino) : IClassFixture<DemoCasinoFixture>
{
    [Fact(DisplayName = "Dado o DemoCasino, quando a resposta de sessão é inspecionada, então cookie HttpOnly e SameSite=Lax e corpo sem segredo")]
    [Trait("Spec", "SPEC-0043:IT-02")]
    public async Task GivenDemoCasinoWhenSessionResponseIsInspectedThenCookieIsHttpOnlyLaxAndBodyHasNoSecret()
    {
        var ct = TestContext.Current.CancellationToken;
        using var raw = casino.Host.CreateRawClient();
        var nickname = CasinoApi.NewNickname();

        using var response = await CasinoApi.EnterAsync(raw, nickname, "BRL", ct);
        var body = await CasinoApi.ReadOkJsonAsync(response, ct);

        var cookie = CasinoApi.SessionSetCookie(response);
        Assert.NotNull(cookie);
        Assert.True(cookie.HttpOnly, "casino_session must be HttpOnly.");
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);

        // The body is exactly the contract: { playerId, nickname }.
        Assert.Equal(
            ["nickname", "playerId"],
            body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);

        var everything = JsonSerializer.Serialize(body) + "\n" + string.Join("\n", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        Assert.DoesNotContain(casino.Host.OperatorSecret, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(casino.Host.OperatorSecret, cookie.Value.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Dado um jogador com sessão, quando consulta /me, então a resposta não traz o segredo nem a chave do operador")]
    [Trait("Spec", "SPEC-0043:IT-02")]
    public async Task GivenPlayerWithSessionWhenAskingMeThenResponseHasNoOperatorCredentials()
    {
        var ct = TestContext.Current.CancellationToken;
        using var browser = casino.Host.CreateBrowserClient();

        using var entered = await CasinoApi.EnterAsync(browser, CasinoApi.NewNickname(), "BRL", ct);
        _ = await CasinoApi.ReadOkJsonAsync(entered, ct);
        using var me = await CasinoApi.MeAsync(browser, ct);
        var text = await me.Content.ReadAsStringAsync(ct);

        Assert.DoesNotContain(casino.Host.OperatorSecret, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(casino.Host.OperatorKeyId, text, StringComparison.OrdinalIgnoreCase);
    }
}
