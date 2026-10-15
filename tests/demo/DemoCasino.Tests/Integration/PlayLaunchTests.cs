using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0045 IT-01 (§5 Segurança, §6): the real DemoCasino against the real Apogeu (the Host in memory on real PostgreSQL
// and Redis, with the SPEC-0020 seed: demo operator, the Pipa enabled, BRL limits). The logged-in player asks
// POST /casino/play/pipa and gets a launchUrl of the Apogeu ({GameBaseUrl}/?lt=…, SPEC-0009@1) that opens the game
// session as the player of the casino session; without a session, 401. The launch is signed on the server: nothing of the
// operator key reaches the browser.
[Collection(ApogeuDependencies.Name)]
public sealed class PlayLaunchTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dados DemoCasino e Apogeu reais, quando o jogador logado lança a Pipa, então recebe uma launchUrl válida que abre a sessão do jogo")]
    [Trait("Spec", "SPEC-0045:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealCasinoAndApogeuWhenLoggedPlayerLaunchesPipaThenLaunchUrlOpensGameSession()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await using var casino = LobbyCasino.Against(apogeu, LobbyCasino.NewClock());
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);
        string playerId;
        using (var me = await CasinoApi.MeAsync(browser, ct))
        {
            playerId = (await CasinoApi.ReadOkJsonAsync(me, ct)).GetProperty("playerId").GetString()!;
        }

        using var response = await CasinoApi.PlayAsync(browser, "pipa", ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        var launchUrl = await CasinoApi.ReadLaunchUrlAsync(response, ct);

        // The launch URL of the Apogeu (SPEC-0009@1): on the game origin, carrying the single-use launch token.
        Assert.Equal(ApogeuApiHost.GameBaseUrl.GetLeftPart(UriPartial.Authority), launchUrl.GetLeftPart(UriPartial.Authority));
        Assert.False(string.IsNullOrEmpty(LaunchUrls.TokenOf(launchUrl)), "The launch URL must carry the launch token (lt).");

        // It opens the game session of the casino's player, in the session currency, in demo mode.
        using var opened = await apogeu.OpenLaunchAsync(launchUrl, ct);
        var session = await CasinoApi.ReadOkJsonAsync(opened, ct);
        Assert.Equal("BRL", session.GetProperty("currency").GetString());
        Assert.Equal("demo", session.GetProperty("mode").GetString());
        var claims = JwtParts.Payload(session.GetProperty("sessionToken").GetString()!);
        Assert.Equal(playerId, claims.GetProperty("sub").GetString());
        Assert.Equal("BRL", claims.GetProperty("cur").GetString());

        // §5: the launch is signed on the server; nothing of the operator key reaches the browser.
        var everything = text + "\n" + string.Join("\n", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        Assert.DoesNotContain(casino.OperatorSecret, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(casino.OperatorKeyId, everything, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Dados DemoCasino e Apogeu reais, quando /casino/play/pipa é pedido sem sessão, então 401")]
    [Trait("Spec", "SPEC-0045:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealCasinoAndApogeuWhenPlayAskedWithoutSessionThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await using var casino = LobbyCasino.Against(apogeu, LobbyCasino.NewClock());
        using var anonymous = casino.CreateBrowserClient();

        using var response = await CasinoApi.PlayAsync(anonymous, "pipa", ct);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 from POST {CasinoApi.PlayPath("pipa")} without a session, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
    }
}
