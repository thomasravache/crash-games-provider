using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Integration;

/// <summary>
/// SPEC-0050: addresses the AppHost hands to the API and to the game, with and without the public tunnel addresses.
/// The model is built without starting it (no containers), so local endpoints are checked by shape.
/// </summary>
public sealed class DevTunnelAddressesTests
{
    private const string GameBaseUrl = "Operators__GameBaseUrl";
    private const string GameOrigin0 = "Cors__GameOrigins__0";
    private const string GameOrigin1 = "Cors__GameOrigins__1";
    private const string GameApiUrl = "VITE_APOGEU_API_URL";

    [Fact(DisplayName = "Dado o modelo do AppHost sem os parâmetros públicos, quando as variáveis da API e do game-pipa são resolvidas, então apontam para os endpoints locais")]
    [Trait("Spec", "SPEC-0050:CH-01")]
    public async Task GivenAppHostWithoutPublicAddressesWhenEnvironmentIsResolvedThenItPointsToLocalEndpoints()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await AppHostModel.BuildAsync([], cancellationToken);

        var api = await AppHostModel.EnvironmentAsync(app, "api", [GameBaseUrl, GameOrigin0, GameOrigin1], cancellationToken);
        var game = await AppHostModel.EnvironmentAsync(app, "game-pipa", [GameApiUrl], cancellationToken);

        Assert.Equal(AppHostModel.Endpoint("game-pipa"), Assert.Contains(GameBaseUrl, api));
        Assert.Equal(AppHostModel.Endpoint("game-pipa"), Assert.Contains(GameOrigin0, api));
        Assert.DoesNotContain(GameOrigin1, api);
        Assert.Equal(AppHostModel.Endpoint("api"), Assert.Contains(GameApiUrl, game));
    }
}
