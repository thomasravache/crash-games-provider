using System.Text.Json;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Integration;

[Collection(RoundsIntegration.Name)]
public sealed class HubSequenceTests(RoundsContainers containers)
{
    [Fact(DisplayName = "Dado um cliente SignalR real (backplane Redis), quando acompanha uma rodada, então recebe Betting, Flying, Sync (≥ 1) e Crashed nessa ordem, com o crashPoint igual ao persistido")]
    [Trait("Spec", "SPEC-0008:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenRealSignalRClientWhenFollowingRoundThenMessagesArriveInOrderWithPersistedCrashPoint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        await using var captured = await HubScenario.CaptureRoundWithSyncAsync(cluster, ct);
        var events = captured.Recorder.Events;
        var round = events.Where(e => e.RoundId == captured.RoundId).ToList();
        var log = captured.Recorder.Describe();

        var betting = Assert.Single(round, e => string.Equals(e.Target, "RoundBetting", StringComparison.Ordinal));
        var flying = Assert.Single(round, e => string.Equals(e.Target, "RoundFlying", StringComparison.Ordinal));
        var crashed = Assert.Single(round, e => string.Equals(e.Target, "RoundCrashed", StringComparison.Ordinal));
        var syncs = round.Where(e => string.Equals(e.Target, "RoundSync", StringComparison.Ordinal)).ToList();
        var position = events.ToList();

        Assert.True(syncs.Count >= 1, $"Expected at least one RoundSync. Received:{Environment.NewLine}{log}");
        Assert.True(
            position.IndexOf(betting) < position.IndexOf(flying)
            && syncs.All(s => position.IndexOf(flying) < position.IndexOf(s) && position.IndexOf(s) < position.IndexOf(crashed)),
            $"Expected Betting, Flying, Sync and Crashed in this order. Received:{Environment.NewLine}{log}");
        Assert.Equal(SalaSettings.SalaId, Json.Text(betting.Payload, "salaId"));

        using var http = captured.Follower.CreateClient();
        var history = await http.GetStringAsync(new Uri("/api/v1/salas/demo/rounds?limit=10", UriKind.Relative), ct);
        using var document = JsonDocument.Parse(history);
        var persisted = document.RootElement.EnumerateArray().SingleOrDefault(r => r.GetProperty("roundId").GetGuid() == captured.RoundId);
        Assert.True(persisted.ValueKind == JsonValueKind.Object, $"Round {captured.RoundId} is not in the persisted history: {history}");
        Assert.Equal(Json.Text(persisted, "crashPoint"), Json.Text(crashed.Payload, "crashPoint"));
    }
}
