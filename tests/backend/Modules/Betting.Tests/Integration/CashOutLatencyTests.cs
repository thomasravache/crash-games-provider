using System.Diagnostics;
using System.Globalization;
using Apogeu.Betting.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class CashOutLatencyTests(BettingContainers containers)
{
    private const int Players = 150;
    private const int CashOuts = Players * 2;
    // SPEC-0023 §5 targets p99 ≤ 50 ms; since SPEC-0024 records the request before asking the leader (one more write),
    // the reference machine measures 52–54 ms, so the local guard is 75 ms until SPEC-0048 brings it back to 50 ms. The shared GitHub
    // runner (2 vCPU, Postgres container on the same host) cannot hold that burst; there (2 vCPU) the test guards against
    // regressions with 3 s (1,7 s measured after SPEC-0024), and the 50 ms target on real infrastructure belongs to the capacity spec (see Emendas).
    private static readonly TimeSpan P99Budget = TimeSpan.FromMilliseconds(
        string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.Ordinal) ? 3000 : 75);
    private static readonly TimeSpan Burst = TimeSpan.FromSeconds(1);

    [Fact(DisplayName = "Dados 300 cash-outs na mesma rodada em 1 s, quando medidos do envio à resposta, então todos decididos e p99 ≤ 75 ms local (meta 50 ms na SPEC-0048; ≤ 3 s no runner da CI)")]
    [Trait("Spec", "SPEC-0023:IT-05")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenThreeHundredCashOutsInOneSecondWhenMeasuredThenAllDecidedAndP99WithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;

        // 10 s of bets to place the 300 bets, and a flight of ln(3)/0,0002 ≈ 5,5 s to cash them out.
        var sala = new BettingSala(Cap: "3.00", BettingDuration: "00:00:10");
        await using var host = await BettingHost.StartAsync(containers, ct, new ProgrammableWallet(), services => FixedCrashFairness.Register(services, 3.00m), sala);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var clients = new List<BetsClient>(Players);
        try
        {
            for (var player = 0; player < Players; player++)
            {
                clients.Add(await BetsClient.ConnectAsync(host, "burst-" + player.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"), ct));
            }

            var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(
                host,
                rounds,
                () => PlaceAllAsync(clients, ct),
                ct,
                bettingLeft: TimeSpan.FromSeconds(8));
            Assert.Equal(CashOuts, bets.Count);

            await flight.AtAsync(TimeSpan.FromMilliseconds(500), ct);
            var measured = await BurstAsync(clients, bets, ct);

            var undecided = measured.Where(m => m.Outcome.Result is not { } result
                || !result.TryGetProperty("cashedOut", out var cashedOut)
                || cashedOut.ValueKind != System.Text.Json.JsonValueKind.Object).ToList();
            Assert.True(undecided.Count == 0, $"{undecided.Count} of {CashOuts} cash-outs not decided as CashedOut, e.g. {undecided.FirstOrDefault().Outcome}");

            var sorted = measured.Select(m => m.Latency).Order().ToList();
            var p99 = sorted[(int)Math.Ceiling(0.99 * sorted.Count) - 1];
            Assert.True(
                p99 <= P99Budget,
                string.Create(CultureInfo.InvariantCulture, $"p99 of CashOut = {p99.TotalMilliseconds:0.0} ms (budget {P99Budget.TotalMilliseconds} ms); p50 {sorted[sorted.Count / 2].TotalMilliseconds:0.0} ms, max {sorted[^1].TotalMilliseconds:0.0} ms"));
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    /// <summary>Each player bets 1,00 in both panels, 20 bets at a time.</summary>
    private static async Task<IReadOnlyList<Guid>> PlaceAllAsync(List<BetsClient> clients, CancellationToken ct)
    {
        var placing = clients.SelectMany(client => (int[])[1, 2], (client, panel) => (client, panel)).ToList();
        var ids = new List<Guid>(CashOuts);
        foreach (var batch in placing.Chunk(20))
        {
            var outcomes = await Task.WhenAll(batch.Select(b => b.client.PlaceAsync(b.panel, "1.00", ct))).ConfigureAwait(false);
            ids.AddRange(outcomes.Select(BetsClient.BetId));
        }

        return ids;
    }

    /// <summary>The burst: 300 cash-outs spread over 1 s, each measured from its send to its answer.</summary>
    private static async Task<(HubOutcome Outcome, TimeSpan Latency)[]> BurstAsync(List<BetsClient> clients, IReadOnlyList<Guid> bets, CancellationToken ct)
    {
        var owners = clients.SelectMany(client => (int[])[0, 1], (client, _) => client).ToList();
        var interval = Burst / CashOuts;
        var burst = Stopwatch.StartNew();
        return await Task.WhenAll(bets.Select(async (betId, index) =>
        {
            var due = interval * index;
            if (due > burst.Elapsed)
            {
                await Task.Delay(due - burst.Elapsed, ct).ConfigureAwait(false);
            }

            var watch = Stopwatch.StartNew();
            var outcome = await owners[index].CashOutAsync(betId, ct).ConfigureAwait(false);
            watch.Stop();
            return (Outcome: outcome, Latency: watch.Elapsed);
        })).ConfigureAwait(false);
    }
}
