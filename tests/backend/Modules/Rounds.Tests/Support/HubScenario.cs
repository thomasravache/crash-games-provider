using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>The hub scenario of IT-03, whose real messages CT-01 also validates against the generated hub contract.</summary>
internal static class HubScenario
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Starts two instances, connects a client to the follower and waits for a round that has at least one
    /// <c>RoundSync</c> (a crash below ≈ 1.01 legitimately has none; then the next round is taken).
    /// </summary>
    public static async Task<CapturedRound> CaptureRoundWithSyncAsync(RoundsCluster cluster, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        var a = cluster.StartHost();
        var b = cluster.StartHost();
        var leader = await Leaders.SingleAsync([a, b], TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        var follower = ReferenceEquals(leader, a) ? b : a;

        var connection = follower.CreateHubConnection();
        var recorder = new HubRecorder(connection);
        await connection.StartAsync(ct).ConfigureAwait(false);
        var snapshot = await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct).ConfigureAwait(false);
        var serverTime = await connection.InvokeAsync<JsonElement>("GetServerTime", ct).ConfigureAwait(false);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var from = recorder.Events.Count;
            var betting = await recorder.WaitForAsync("RoundBetting", RoundTimeout, from).ConfigureAwait(false);
            await recorder.WaitForAsync("RoundCrashed", e => e.RoundId == betting.RoundId, RoundTimeout, from).ConfigureAwait(false);
            if (recorder.Of("RoundSync", betting.RoundId).Count > 0)
            {
                return new CapturedRound(follower, connection, recorder, betting.RoundId, snapshot.Clone(), serverTime.Clone());
            }
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        Assert.Fail($"No round with a RoundSync in 8 rounds. Received:{Environment.NewLine}{recorder.Describe()}");
        return null!;
    }
}
