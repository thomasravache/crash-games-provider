using System.Text.Json;
using Apogeu.DemoCasino.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Unit;

// SPEC-0045 UT-01 (§4, Emenda v1): the body of the signed launch the casino sends to the Apogeu is exactly the one of
// SPEC-0009@1: playerId, nickname and currency of the casino session, gameCode of the route, mode "demo" and language
// "pt-BR", and no other field (no returnUrl). Exercised through the BFF on the in-memory DemoCasino with a capturing
// stand-in for the Apogeu network (no external dependency), so the test holds whatever builder type the implementation
// uses; the request is read as it left the casino's HTTP pipeline, after the signing step.
public sealed class LaunchBodyTests
{
    private const string LaunchPath = "/api/v1/operator/launch";

    private static readonly string[] LaunchFields = ["currency", "gameCode", "language", "mode", "nickname", "playerId"];

    public static TheoryData<string> GameCodes => ["pipa", "aviao"];

    [Theory(DisplayName = "Dada a sessão do cassino, quando o corpo do lançamento é montado, então traz playerId, nickname e currency da sessão, gameCode da rota, mode demo e language pt-BR, e nada mais")]
    [Trait("Spec", "SPEC-0045:UT-01")]
    [MemberData(nameof(GameCodes))]
    public async Task GivenCasinoSessionWhenLaunchBodyIsBuiltThenItIsTheSpec0009Body(string gameCode)
    {
        var ct = TestContext.Current.CancellationToken;
        using var apogeu = new CapturingHandler(CapturingHandler.LaunchResponse);
        await using var casino = LobbyCasino.Against(apogeu);
        using var browser = casino.CreateBrowserClient();
        var nickname = CasinoApi.NewNickname();
        string playerId;
        using (var entered = await CasinoApi.EnterAsync(browser, nickname, "BRL", ct))
        {
            playerId = (await CasinoApi.ReadOkJsonAsync(entered, ct)).GetProperty("playerId").GetString()!;
        }

        using var response = await CasinoApi.PlayAsync(browser, gameCode, ct);

        var launch = Assert.Single(apogeu.Requests, request => string.Equals(request.Uri.AbsolutePath, LaunchPath, StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, launch.Method);
        using var json = JsonDocument.Parse(launch.Body);
        var body = json.RootElement;
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.Equal(LaunchFields, body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(playerId, body.GetProperty("playerId").GetString());
        Assert.Equal(nickname, body.GetProperty("nickname").GetString());
        Assert.Equal("BRL", body.GetProperty("currency").GetString());
        Assert.Equal(gameCode, body.GetProperty("gameCode").GetString());
        Assert.Equal("demo", body.GetProperty("mode").GetString());
        Assert.Equal("pt-BR", body.GetProperty("language").GetString());
    }
}
