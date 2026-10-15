using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0043 IT-01: the real DemoCasino (WebApplicationFactory) through a browser-like client that keeps and sends back
// the cookies: enter, ask who I am, leave.
public sealed class CasinoSessionFlowTests(DemoCasinoFixture casino) : IClassFixture<DemoCasinoFixture>
{
    [Fact(DisplayName = "Dado o DemoCasino real, quando entra, consulta /me e sai, então cookie HttpOnly, 200 e depois 401")]
    [Trait("Spec", "SPEC-0043:IT-01")]
    public async Task GivenRealDemoCasinoWhenEnteringAskingMeAndLeavingThenCookieOkThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var browser = casino.Host.CreateBrowserClient();
        var nickname = CasinoApi.NewNickname();

        // Control: without the cookie there is no session.
        using (var anonymous = await CasinoApi.MeAsync(browser, ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        using var entered = await CasinoApi.EnterAsync(browser, nickname, "BRL", ct);
        var session = await CasinoApi.ReadOkJsonAsync(entered, ct);
        var cookie = CasinoApi.SessionSetCookie(entered);
        Assert.NotNull(cookie);
        Assert.True(cookie.HttpOnly, "casino_session must be HttpOnly.");
        Assert.False(string.IsNullOrEmpty(cookie.Value.Value), "casino_session must carry a value.");
        var playerId = session.GetProperty("playerId").GetString();

        using (var me = await CasinoApi.MeAsync(browser, ct))
        {
            var body = await CasinoApi.ReadOkJsonAsync(me, ct);
            Assert.Equal(playerId, body.GetProperty("playerId").GetString());
            Assert.Equal(nickname, body.GetProperty("nickname").GetString());
            Assert.Equal("BRL", body.GetProperty("currency").GetString());
        }

        using (var logout = await CasinoApi.LogoutAsync(browser, ct))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            var removal = CasinoApi.SessionSetCookie(logout);
            Assert.NotNull(removal);
            Assert.True(IsRemoval(removal), $"The logout must remove casino_session (expired or empty), got '{removal}'.");
        }

        using (var afterLogout = await CasinoApi.MeAsync(browser, ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        }
    }

    [Fact(DisplayName = "Dado um cookie casino_session adulterado, quando consulta /me, então 401")]
    [Trait("Spec", "SPEC-0043:IT-01")]
    public async Task GivenTamperedSessionCookieWhenAskingMeThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var raw = casino.Host.CreateRawClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(CasinoApi.MePath, UriKind.Relative));
        request.Headers.Add(HeaderNames.Cookie, $"{CasinoApi.SessionCookie}=forjado");
        using var response = await raw.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static bool IsRemoval(SetCookieHeaderValue cookie) =>
        (cookie.Expires is { } expires && expires <= DateTimeOffset.UtcNow)
        || cookie.MaxAge is { } maxAge && maxAge <= TimeSpan.Zero
        || string.IsNullOrEmpty(cookie.Value.Value);
}
