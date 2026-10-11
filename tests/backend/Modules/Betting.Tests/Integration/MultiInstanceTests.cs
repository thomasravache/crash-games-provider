using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class MultiInstanceTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dadas duas instâncias do Host com o mesmo Redis e PostgreSQL, quando uma aposta chega na instância que não conduz a sala durante a fase de apostas, então fica placed na rodada aberta")]
    [Trait("Spec", "SPEC-0022:IT-11")]
    [Trait("Category", "Integration")]
    public async Task GivenTwoInstancesWhenBetReachesNonLeaderDuringBettingThenItIsPlacedInOpenRound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var first = await BettingHost.StartAsync(containers, ct);
        await using var second = await BettingHost.StartPeerAsync(first);
        await Eventually.HoldsAsync(() => first.IsLeading || second.IsLeading, TimeSpan.FromSeconds(30), () => "one instance leading the room");
        var follower = first.IsLeading ? second : first;
        Assert.False(follower.IsLeading);

        // The phases as the follower's clients see them (SignalR Redis backplane): no wait on the follower's own state.
        await using var watcher = follower.CreateRoundsConnection();
        var recorder = new HubRecorder(watcher);
        await watcher.StartAsync(ct);
        await watcher.InvokeAsync<JsonElement>("JoinSala", BettingSala.SalaId, ct);
        HubEvent? betting = null;
        for (var attempt = 0; attempt < 10 && betting is null; attempt++)
        {
            var candidate = await recorder.WaitForAsync("RoundBetting", RoundTimeout, recorder.Events.Count);
            if (Json.Instant(candidate.Payload, "bettingEndsAt") - DateTimeOffset.UtcNow >= TimeSpan.FromSeconds(1.5))
            {
                betting = candidate;
            }
        }

        Assert.NotNull(betting);
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        await using var bets = await BetsClient.ConnectAsync(follower, "player-" + Guid.NewGuid().ToString("N"), ct);
        var accepted = (await bets.PlaceAsync(panel: 1, amount: "10.00", ct)).Value;

        Assert.Equal("placed", Json.Text(accepted, "status"));
        Assert.Equal(betting.RoundId, accepted.GetProperty("roundId").GetGuid());
        Assert.Equal(betting.Payload.GetProperty("roundNumber").GetInt64(), accepted.GetProperty("roundNumber").GetInt64());
    }
}
