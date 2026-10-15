using System.Text.Json;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// IT-05 (SPEC-0025 §6, Emenda v4): on the real Host, a room whose previous round had bets and that has no current round in
/// the feed (<see cref="SilencedRoundFeed"/>) answers <c>JoinSalaBets</c> with an empty snapshot — never the bets nor the
/// totals of the previous round.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class LiveBetsWithoutRoundTests(BettingContainers containers)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "Dada uma sala cuja rodada anterior teve apostas e que está sem rodada corrente no feed, quando um jogador chama JoinSalaBets, então recebe um snapshot vazio, sem apostas nem totais da rodada anterior")]
    [Trait("Spec", "SPEC-0025:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenRoomWithoutCurrentRoundAfterARoundWithBetsWhenJoiningSalaBetsThenSnapshotIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;

        // 8 s of bets, so the bets and the snapshot of the round happen in its betting phase.
        await using var host = await BettingHost.StartAsync(
            containers,
            ct,
            services: services => services.Decorate<IRoundFeed>((inner, _) => new SilencedRoundFeed(inner)),
            sala: new BettingSala(BettingDuration: "00:00:08"));
        var feed = Assert.IsType<SilencedRoundFeed>(host.Services.GetRequiredService<IRoundFeed>());
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var marcos = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct, nickname: "Marcos");

        // A round with bets: 10,00 and 25,00, seen in the room's live bets.
        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(5));
        _ = BetsClient.BetId(await marcos.PlaceAsync(panel: 1, amount: "10.00", ct));
        _ = BetsClient.BetId(await marcos.PlaceAsync(panel: 2, amount: "25.00", ct));
        var withBets = await SnapshotAsync(marcos, s => s.GetProperty("totalBets").GetInt32() == 2, ct);
        Assert.Equal(betting.RoundId, withBets.GetProperty("roundId").GetGuid());

        // The feed no longer has a current round for the room.
        feed.Silence();

        await using var beatriz = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct, nickname: "Beatriz");
        var joined = await beatriz.JoinSalaBetsAsync(ct);

        Assert.True(joined.Error is null && joined.Result is { ValueKind: JsonValueKind.Object }, $"JoinSalaBets without a current round: {joined}");
        var snapshot = joined.Value;
        Assert.Equal(0, snapshot.GetProperty("totalBets").GetInt32());
        Assert.Equal(0, snapshot.GetProperty("totalPlayers").GetInt32());
        Assert.Equal(0.00m, Json.Decimal(snapshot, "totalWagered"));
        Assert.Empty(snapshot.GetProperty("bets").EnumerateArray());
        Assert.NotEqual(betting.RoundId, snapshot.GetProperty("roundId").GetGuid());
    }

    /// <summary>The snapshot of <c>JoinSalaBets</c> once it satisfies <paramref name="accept"/> (the projection may lag the bet).</summary>
    private static async Task<JsonElement> SnapshotAsync(BetsClient client, Func<JsonElement, bool> accept, CancellationToken ct)
    {
        var outcome = await Eventually.ResultAsync(
            async () => await client.JoinSalaBetsAsync(ct).ConfigureAwait(false),
            o => o.Result is { } snapshot && accept(snapshot),
            Timeout,
            () => $"the snapshot of JoinSalaBets; got {client.Outcomes.LastOrDefault(o => string.Equals(o.Method, "JoinSalaBets", StringComparison.Ordinal))}").ConfigureAwait(false);
        return outcome.Value;
    }
}
