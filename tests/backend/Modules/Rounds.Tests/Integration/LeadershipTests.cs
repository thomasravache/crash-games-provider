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

    // Long flights (up to ln(2)/0.00002 ≈ 34.7 s): the old leader is flying, sending RoundSync every 200 ms, when it loses Redis.
    private static readonly SalaSettings LongFlights = new(GrowthK: "0.00002", Cap: "2.00");

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

        // G4 review: every takeover, not only the median, meets the NFR (≤ 6 s).
        var samples = await MeasureTakeoversAsync(cluster, hosts, numbers.Max(), ct);
        var slowest = samples.Max();
        var described = string.Join(", ", samples.Select(s => s.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms"));
        Assert.True(slowest <= TakeoverLimit, $"A takeover took {slowest.TotalMilliseconds:F0} ms > {TakeoverLimit.TotalSeconds} s. Samples: {described}");
    }

    // From the G4 review of SPEC-0008: the leader loses Redis the ungraceful way (its lock connection hangs; no release, no
    // DisposeAsync). The other instance takes over within 6 s and the old leader conducts nothing once it does.
    [Fact(DisplayName = "Dado o líder que perde o Redis sem liberar o lock (conexão travada), quando a outra instância assume, então assume em ≤ 6 s e o líder antigo não transiciona nem transmite nada depois disso")]
    [Trait("Spec", "SPEC-0008:IT-01")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenLeaderLosingRedisUngracefullyWhenAnotherTakesOverThenWithinSixSecondsAndWithoutOverlap()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, LongFlights, ct);
        await using var proxy = FreezableTcpProxy.Start(cluster.RedisConnectionString);
        AttributedBroadcaster? sentByOld = null;
        var old = cluster.StartHost(new RoundsHostSetup
        {
            Salas = new Dictionary<string, SalaSettings>(StringComparer.Ordinal) { [SalaSettings.SalaId] = LongFlights },
            LeaderRedis = proxy.ConnectionString,
            Services = services => AttributedBroadcaster.Decorate(services, b => sentByOld = b),
        });
        await Eventually.HoldsAsync(() => old.IsLeading, TimeSpan.FromSeconds(30), () => $"{old.Name} leading");
        var successor = cluster.StartHost();
        IReadOnlyList<RoundsHost> hosts = [old, successor];

        await using var connection = successor.CreateHubConnection();
        var recorder = new HubRecorder(connection);
        await connection.StartAsync(ct);
        await connection.InvokeAsync<System.Text.Json.JsonElement>("JoinSala", SalaSettings.SalaId, ct);
        await recorder.WaitForAsync("RoundFlying", RoundTimeout);

        var overlaps = new List<string>();
        using var sampling = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sampler = SampleLeadershipAsync(hosts, overlaps, sampling.Token);

        var watch = Stopwatch.StartNew();
        proxy.Freeze();
        await Eventually.HoldsAsync(() => successor.IsLeading, TimeSpan.FromSeconds(30), () => $"{successor.Name} taking over");
        var takeover = watch.Elapsed;
        var successorLeadsAt = DateTimeOffset.UtcNow;

        // Leave the old leader time to (wrongly) keep conducting: its renewal hangs until the Redis client times out.
        await Task.Delay(TimeSpan.FromSeconds(4), ct);
        await sampling.CancelAsync();
        await sampler;

        Assert.True(takeover <= TakeoverLimit, $"Takeover after an ungraceful loss took {takeover.TotalMilliseconds:F0} ms > {TakeoverLimit.TotalSeconds} s.");
        Assert.True(overlaps.Count == 0, $"Two instances led the room at the same time: {string.Join("; ", overlaps)}");
        Assert.NotNull(sentByOld);
        var late = sentByOld.Sent.Where(m => m.SentAt >= successorLeadsAt).ToList();
        Assert.True(
            late.Count == 0,
            $"The old leader {old.Name} kept conducting after {successor.Name} took over at {successorLeadsAt:O}:{Environment.NewLine}{string.Join(Environment.NewLine, late)}");
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
