using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Performance;
using Xunit;

namespace Apogeu.Fairness.Tests.Integration;

/// <summary>IT-02: continuity after a restart and the cost of obtaining a seed; timed, so it runs without parallelism.</summary>
[Collection(TimedPostgresTests.Name)]
public sealed class FairnessRestartTimingTests(PostgreSqlContainerFixture postgres)
{
    private const int Length = 50_000;
    private const int Samples = 15;
    private const int WarmUps = 5;
    private static readonly Rtp DemoRtp = new(0.97m);
    private static readonly Multiplier DefaultCap = new(10000.00m);
    // 20 ms on the reference machine, × the machine factor elsewhere (SPEC-0052).
    private static readonly (TimeSpan Budget, string Source) SeedBudget =
        PerformanceBudget.Resolve(TimeSpan.FromMilliseconds(20), Environment.GetEnvironmentVariable);

    [Fact(DisplayName = "Dado um processo que avança 3 rodadas e é reiniciado, quando NextRoundAsync é chamado, então o índice é 4 (sem repetir nem pular semente); e, após aquecimento, a mediana de 15 obtenções da semente a ~10.000 hashes do checkpoint é ≤ 20 ms na máquina de referência")]
    [Trait("Spec", "SPEC-0007:IT-02")]
    [Trait("Category", "Performance")]
    [Trait("Category", "Integration")]
    public async Task GivenProcessRestartedAfterThreeRoundsWhenNextRoundThenIndexIsFourAndMedianSeedRetrievalIsWithinTwentyMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        var sala = new SalaId(Guid.NewGuid());
        ChainInfo chain;
        RoundSeedCommitment third;

        await using (var first = await FairnessTestHost.StartAsync(connectionString, keys.Directory, logCapture: null, ct))
        {
            chain = await first.RunAsync(fairness => fairness.CreateChainAsync(sala, Length, ct));
            for (var r = 1; r <= 2; r++)
            {
                await first.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
            }

            third = await first.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
        }

        Assert.Equal(3, third.RoundIndex);

        await using var restarted = await FairnessTestHost.StartAsync(connectionString, keys.Directory, logCapture: null, ct);

        // Correctness, not timed: the restarted process issues round 4.
        var fourth = await restarted.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));

        Assert.Equal(chain.ChainId, fourth.ChainId);
        Assert.Equal(4, fourth.RoundIndex);

        // Neither repeated nor skipped: the commitment of round 4 is SHA256(s_4) = s_3, the seed of round 3.
        var revealedThird = await restarted.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, 3, DemoRtp, DefaultCap, ct));
        var revealedFourth = await restarted.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, 4, DemoRtp, DefaultCap, ct));
        Assert.Equal(revealedThird.Seed.ToArray(), fourth.Commitment.ToArray());
        Assert.Equal(SHA256.HashData(revealedFourth.Seed.ToArray()), fourth.Commitment.ToArray());
        Assert.Equal(third.Commitment.ToArray(), SHA256.HashData(revealedThird.Seed.ToArray()));
        Assert.NotEqual(third.Commitment.ToArray(), fourth.Commitment.ToArray());

        // Round 4 uses h_49996: 9,996 hashes from the checkpoint h_40000, the worst case "~10,000 hashes" of §5.
        var link = SeedChain.LinkIndexOfRound(Length, 4);
        var hashes = link - SeedChain.CheckpointOf(link);
        Assert.InRange(hashes, 9_900, SeedChain.CheckpointInterval - 1);

        await AssertMedianSeedRetrievalAsync(restarted, chain.ChainId, revealedFourth.Seed, hashes, ct);
    }

    /// <summary>After warm-up, the median of 15 seed retrievals (RevealAsync of round 4, each in its own DI scope) is ≤ 20 ms on the reference machine.</summary>
    private static async Task AssertMedianSeedRetrievalAsync(FairnessTestHost host, ChainId chain, Seed expected, int hashes, CancellationToken ct)
    {
        for (var i = 0; i < WarmUps; i++)
        {
            await host.RunAsync(fairness => fairness.RevealAsync(chain, 4, DemoRtp, DefaultCap, ct)).ConfigureAwait(false);
        }

        var samples = new List<double>(Samples);
        for (var i = 0; i < Samples; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var revealed = await host.RunAsync(fairness => fairness.RevealAsync(chain, 4, DemoRtp, DefaultCap, ct)).ConfigureAwait(false);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            Assert.Equal(expected.ToArray(), revealed.Seed.ToArray());
        }

        var median = samples.Order().ElementAt(Samples / 2);
        var all = string.Join(", ", samples.Select(sample => sample.ToString("F2", CultureInfo.InvariantCulture)));
        Assert.True(
            median <= SeedBudget.Budget.TotalMilliseconds,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Median seed retrieval ({hashes} hashes from the checkpoint) took {median:F2} ms, above the budget of {SeedBudget.Budget.TotalMilliseconds} ms from {SeedBudget.Source}. Samples (ms): {all}"));
    }
}
