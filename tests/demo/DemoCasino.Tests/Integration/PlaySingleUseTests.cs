using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0045 IT-02 (§5 Segurança, §6.1 "Uso único"): the launchUrl the casino hands out is single use. Opened once, it
// starts the game session; opened again, the Apogeu refuses it (401 LAUNCH_TOKEN_USED of SPEC-0009@1, which the game
// shows as "Sessão expirada").
[Collection(ApogeuDependencies.Name)]
public sealed class PlaySingleUseTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dada uma launchUrl do cassino já usada, quando aberta de novo, então a Apogeu recusa (sessão expirada)")]
    [Trait("Spec", "SPEC-0045:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenUsedLaunchUrlWhenOpenedAgainThenApogeuRefuses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await using var casino = LobbyCasino.Against(apogeu, LobbyCasino.NewClock());
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);
        using var launched = await CasinoApi.PlayAsync(browser, "pipa", ct);
        var launchUrl = await CasinoApi.ReadLaunchUrlAsync(launched, ct);

        // Control: the first opening starts the game session.
        using (var first = await apogeu.OpenLaunchAsync(launchUrl, ct))
        {
            _ = await CasinoApi.ReadOkJsonAsync(first, ct);
        }

        using var second = await apogeu.OpenLaunchAsync(launchUrl, ct);

        await CasinoApi.AssertErrorAsync(second, HttpStatusCode.Unauthorized, "LAUNCH_TOKEN_USED", ct);
    }
}
