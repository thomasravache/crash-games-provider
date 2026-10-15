using System.Diagnostics;
using System.Globalization;
using Apogeu.Betting.Tests.Support;
using Apogeu.TestKit.Performance;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class PlaceBetLatencyTests(BettingContainers containers)
{
    private const int Bets = 300;
    private const int WarmUp = 10;
    // 100 ms on the reference machine, × the machine factor elsewhere (SPEC-0052).
    private static readonly (TimeSpan Budget, string Source) P99Budget =
        PerformanceBudget.Resolve(TimeSpan.FromMilliseconds(100), Environment.GetEnvironmentVariable);

    [Fact(DisplayName = "Dadas 300 apostas sequenciais (carteira em memória), quando medidas do envio à resposta, então p99 ≤ 100 ms na máquina de referência")]
    [Trait("Spec", "SPEC-0022:IT-06")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenThreeHundredSequentialBetsWhenMeasuredThenP99IsWithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1));
        var samples = new List<TimeSpan>(Bets);

        // Each player bets in both panels; the round phase does not matter (placed or queued).
        // The first bets only warm up the host (JIT, pools) and are not measured.
        for (var player = 0; player < (WarmUp / 2) + (Bets / 2); player++)
        {
            await using var client = await BetsClient.ConnectAsync(host, "perf-" + player.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"), ct);
            foreach (var panel in (int[])[1, 2])
            {
                var watch = Stopwatch.StartNew();
                var outcome = await client.PlaceAsync(panel, "1.00", ct);
                watch.Stop();
                _ = outcome.Value;
                if (player >= WarmUp / 2)
                {
                    samples.Add(watch.Elapsed);
                }
            }
        }

        Assert.Equal(Bets, samples.Count);
        var sorted = samples.Order().ToList();
        var p99 = sorted[(int)Math.Ceiling(0.99 * sorted.Count) - 1];
        Assert.True(
            p99 <= P99Budget.Budget,
            string.Create(CultureInfo.InvariantCulture, $"p99 of PlaceBet = {p99.TotalMilliseconds:0.0} ms (budget {P99Budget.Budget.TotalMilliseconds} ms from {P99Budget.Source}); p50 {sorted[sorted.Count / 2].TotalMilliseconds:0.0} ms, max {sorted[^1].TotalMilliseconds:0.0} ms"));
    }
}
