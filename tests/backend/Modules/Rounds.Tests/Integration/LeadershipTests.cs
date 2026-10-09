using System.Diagnostics;
using System.Globalization;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Rounds.Tests.Integration;

[Collection(RoundsIntegration.Name)]
public sealed class LeadershipTests(RoundsContainers containers)
{
    private static readonly TimeSpan TakeoverLimit = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);
    private const int Rounds = 5;
    private const int TakeoverSamples = 3;

    [Fact(DisplayName = "Dadas duas instâncias do condutor com Redis real, quando rodam 5 rodadas, então só uma conduz e os números de rodada são 1..5 sem repetição; ao matar a líder, a outra assume em ≤ 6 s")]
    [Trait("Spec", "SPEC-0008:IT-01")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenTwoConductorsWhenFiveRoundsRunThenOnlyOneLeadsAndAnotherTakesOverWithinSixSeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var hosts = new List<RoundsHost> { cluster.StartHost(), cluster.StartHost() };
        var leader = await Leaders.SingleAsync(hosts, TimeSpan.FromSeconds(30));
        var follower = hosts.Single(h => !ReferenceEquals(h, leader));

        // The client follows the room on the follower: every message comes through the backplane.
        await using var connection = follower.CreateHubConnection();
        var recorder = new HubRecorder(connection);
        await connection.StartAsync(ct);
        await connection.InvokeAsync<System.Text.Json.JsonElement>("JoinSala", SalaSettings.SalaId, ct);

        var overlaps = new List<string>();
        using var sampling = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sampler = SampleLeadershipAsync(hosts, overlaps, sampling.Token);

        for (var crashed = 0; crashed < Rounds;)
        {
            var from = recorder.Events.Count;
            var betting = await recorder.WaitForAsync("RoundBetting", RoundTimeout, from);
            await recorder.WaitForAsync("RoundCrashed", e => e.RoundId == betting.RoundId, RoundTimeout, from);
            crashed++;
        }

        await sampling.CancelAsync();
        await sampler;

        var numbers = recorder.Of("RoundBetting").Select(e => e.Payload.GetProperty("roundNumber").GetInt64()).ToList();
        Assert.True(overlaps.Count == 0, $"Two instances led the room at the same time: {string.Join("; ", overlaps)}");
        Assert.True(leader.IsLeading && !follower.IsLeading, $"The leader changed with both instances alive ({leader.Name} leads: {leader.IsLeading}).");
        Assert.True(
            numbers.Distinct().Count() == numbers.Count && numbers.Take(Rounds).SequenceEqual([1, 2, 3, 4, 5]),
            $"Expected the round numbers 1..5 without repetition, got [{string.Join(", ", numbers)}].");

        var samples = await MeasureTakeoversAsync(cluster, hosts, numbers.Max(), ct);
        var median = samples.Order().ElementAt(samples.Count / 2);
        var described = string.Join(", ", samples.Select(s => s.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms"));
        Assert.True(median <= TakeoverLimit, $"Median takeover {median.TotalMilliseconds:F0} ms > {TakeoverLimit.TotalSeconds} s. Samples: {described}");
    }

    /// <summary>Kills the leader <see cref="TakeoverSamples"/> times (a fresh follower each time) and measures each takeover.</summary>
    private static async Task<IReadOnlyList<TimeSpan>> MeasureTakeoversAsync(
        RoundsCluster cluster,
        List<RoundsHost> hosts,
        long lastNumber,
        CancellationToken ct)
    {
        var samples = new List<TimeSpan>();
        var seen = new HashSet<long>();
        for (var sample = 0; sample < TakeoverSamples; sample++)
        {
            var leader = await Leaders.SingleAsync(hosts, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            var survivor = hosts.Single(h => !ReferenceEquals(h, leader));
            var connection = survivor.CreateHubConnection();
            await using var connectionDisposal = connection.ConfigureAwait(false);
            var recorder = new HubRecorder(connection);
            await connection.StartAsync(ct).ConfigureAwait(false);
            await connection.InvokeAsync<System.Text.Json.JsonElement>("JoinSala", SalaSettings.SalaId, ct).ConfigureAwait(false);

            var watch = Stopwatch.StartNew();
            await leader.DisposeAsync().ConfigureAwait(false);
            hosts.Remove(leader);
            await Eventually.HoldsAsync(() => survivor.IsLeading, TimeSpan.FromSeconds(30), () => $"{survivor.Name} taking over").ConfigureAwait(false);
            samples.Add(watch.Elapsed);

            // The new leader continues the numbering: no round number is repeated.
            var betting = await recorder.WaitForAsync("RoundBetting", RoundTimeout).ConfigureAwait(false);
            var number = betting.Payload.GetProperty("roundNumber").GetInt64();
            Assert.True(number > lastNumber && seen.Add(number), $"After the takeover the round number {number} repeats (last was {lastNumber}).");
            lastNumber = number;

            hosts.Add(cluster.StartHost());
        }

        return samples;
    }

    private static async Task SampleLeadershipAsync(IReadOnlyList<RoundsHost> hosts, List<string> overlaps, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (hosts.Count(h => h.IsLeading) > 1)
                {
                    overlaps.Add($"{DateTimeOffset.UtcNow:O}: {string.Join(", ", hosts.Where(h => h.IsLeading).Select(h => h.Name))}");
                }

                await Task.Delay(50, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Sampling ends with the five rounds.
        }
    }
}
