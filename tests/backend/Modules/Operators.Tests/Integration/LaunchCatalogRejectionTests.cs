using System.Net;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;
using static Apogeu.Operators.Tests.Support.CatalogExpectations;

namespace Apogeu.Operators.Tests.Integration;

/// <summary>IT-02: on the real Host, the launch refuses unavailable games and currencies without limits (SPEC-0020 §6).</summary>
[Collection(OperatorsApiDependencies.Name)]
public sealed class LaunchCatalogRejectionTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    /// <summary>A game of the catalog that the fixture leaves disabled for the demo operator.</summary>
    internal static readonly GameDefinition DisabledGame = new(
        new GameCode("jogo-desabilitado"),
        "Jogo desabilitado",
        "game-desabilitado",
        "desabilitado",
        new SalaDefaults(0.97m, new Multiplier(10_000.00m), 0.00006, 6, 3),
        [Brl]);

    [Fact(DisplayName = "Dado o Host real, quando o lançamento recebe gameCode:\"aviao\", então 400 GAME_NOT_AVAILABLE")]
    [Trait("Spec", "SPEC-0020:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostWhenLaunchingUnknownGameThenGameNotAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        using var client = host.CreateClient();

        using var request = CatalogApi.SignedLaunch(CatalogApi.LaunchBody("aviao", "BRL"), host.Key, host.UnixNow());
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.BadRequest, "GAME_NOT_AVAILABLE", ct);
    }

    [Fact(DisplayName = "Dado o Host real e um jogo do catálogo desabilitado para o operador, quando o lançamento recebe esse jogo, então 400 GAME_NOT_AVAILABLE")]
    [Trait("Spec", "SPEC-0020:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostWhenLaunchingDisabledGameThenGameNotAvailable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        await AddDisabledGameAsync(host, ct);
        using var client = host.CreateClient();

        using var request = CatalogApi.SignedLaunch(CatalogApi.LaunchBody(DisabledGame.Code.Value, "BRL"), host.Key, host.UnixNow());
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.BadRequest, "GAME_NOT_AVAILABLE", ct);
    }

    [Fact(DisplayName = "Dado o Host real, quando o lançamento recebe currency:\"USD\", então 400 CURRENCY_NOT_ENABLED")]
    [Trait("Spec", "SPEC-0020:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostWhenLaunchingInUsdThenCurrencyNotEnabled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        using var client = host.CreateClient();

        using var request = CatalogApi.SignedLaunch(CatalogApi.LaunchBody("pipa", "USD"), host.Key, host.UnixNow());
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.BadRequest, "CURRENCY_NOT_ENABLED", ct);
    }

    /// <summary>The test fixture of §7: a game registered in the catalog and disabled for the demo operator.</summary>
    internal static Task AddDisabledGameAsync(OperatorsApiHost host, CancellationToken ct) =>
        host.WithCatalogStoreAsync(async store =>
        {
            await store.EnsureGameAsync(DisabledGame, ct).ConfigureAwait(false);
            await store.SetGameEnabledAsync(Demo, DisabledGame.Code, enabled: false, ct).ConfigureAwait(false);
        });
}
