using System.Net;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Security.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// IT-04 (SPEC-0025 §6, Emenda v3): <c>GET /api/v1/me/bets</c> on the real Host — an absent <c>limit</c> is 20, and a
/// <c>before</c> that is not a cursor issued by the API is <c>400 INVALID_CURSOR</c> in the error envelope of the other REST
/// endpoints.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class MyBetsDefaultsTests(BettingContainers containers)
{
    private const int BetCount = 25;

    [Fact(DisplayName = "Dado um jogador com 25 apostas, quando GET /api/v1/me/bets sem limit, então recebe as 20 mais recentes e nextCursor que leva às 5 restantes")]
    [Trait("Spec", "SPEC-0025:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenTwentyFiveBetsWhenGettingMyBetsWithoutLimitThenTwentyItemsAndNextCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var written = await WriteBetsAsync(host, player, ct);
        using var client = host.CreatePlayerHttpClient(player);

        var (items, next) = await MyBetsApi.PageAsync(client, limit: null, before: null, ct);

        Assert.Equal(20, items.Count);
        var mostRecentFirst = written.AsEnumerable().Reverse().ToList();
        Assert.Equal(mostRecentFirst.Take(20), items.Select(i => i.GetProperty("betId").GetGuid()));
        Assert.NotNull(next);

        var (rest, end) = await MyBetsApi.PageAsync(client, limit: null, before: next, ct);
        Assert.Equal(mostRecentFirst.Skip(20), rest.Select(i => i.GetProperty("betId").GetGuid()));
        Assert.Null(end);
    }

    [Theory(DisplayName = "Dado um before que não é cursor emitido pela API (não base64url ou de tamanho errado), quando GET /api/v1/me/bets, então 400 INVALID_CURSOR no envelope de erro")]
    [Trait("Spec", "SPEC-0025:IT-04")]
    [Trait("Category", "Integration")]
    [InlineData("não-é-cursor!")]
    [InlineData("%%%%")]
    [InlineData("AAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task GivenMalformedCursorWhenGettingMyBetsThenInvalidCursor(string before)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        using var client = host.CreatePlayerHttpClient("player-" + Guid.NewGuid().ToString("N"));

        // Without limit (the default) and with a valid limit: the cursor is rejected either way.
        using var withoutLimit = await client.GetAsync(MyBetsApi.PageUri(null, before), ct);
        await ErrorBodies.AssertErrorAsync(withoutLimit, HttpStatusCode.BadRequest, "INVALID_CURSOR", ct);
        using var withLimit = await client.GetAsync(MyBetsApi.PageUri(10, before), ct);
        await ErrorBodies.AssertErrorAsync(withLimit, HttpStatusCode.BadRequest, "INVALID_CURSOR", ct);
    }

    /// <summary>
    /// <see cref="BetCount"/> placed bets of <paramref name="player"/>, one second apart and each in a round of its own, written
    /// through the module's own store (reserved, then confirmed) as PlaceBet does after the debit; oldest first.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> WriteBetsAsync(BettingHost host, string player, CancellationToken ct)
    {
        var rules = new BetRules(MinBet: 1.00m, MaxBet: 1000.00m, MaxAutoCashOut: 2.00m);
        var owner = new BetOwner(new OperatorId(BettingSessions.Operator), new PlayerId(player), new Currency(BettingSessions.Currency));
        var first = DateTimeOffset.UtcNow.AddMinutes(-1);
        var ids = new List<Guid>(BetCount);
        for (var i = 0; i < BetCount; i++)
        {
            var scope = host.Services.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var store = scope.ServiceProvider.GetRequiredService<IBetStore>();
                var at = first.AddSeconds(i);
                var window = new RoundWindow(new RoundId(Guid.NewGuid()), 1_000 + i, DateTimeOffset.UtcNow.AddMinutes(5), RoundPhase.Betting);
                var placement = Bet.Place(Guid.NewGuid(), owner, new BetRequest(Guid.NewGuid(), (i % 2) + 1, 1.00m, null), rules, window, at, []);
                var bet = Assert.IsType<BetPlacementAccepted>(placement).Bet;
                await store.ReserveAsync(bet, BettingSala.SalaId, at, ct).ConfigureAwait(false);
                Assert.Equal(BetConfirmation.Confirmed, await store.ConfirmAsync(bet.Id, FakeOperatorWallet.OpeningBalance - 1.00m, ct).ConfigureAwait(false));
                ids.Add(bet.Id);
            }
        }

        return ids;
    }
}
