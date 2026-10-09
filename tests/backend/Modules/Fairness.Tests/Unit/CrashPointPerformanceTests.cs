using System.Diagnostics;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Randomness;
using Xunit;

namespace Apogeu.Fairness.Tests.Unit;

/// <summary>The timed unit test of the crash-point calculation; it runs without parallelism with the other tests.</summary>
[Collection(TimedUnitTests.Name)]
public sealed class CrashPointPerformanceTests
{
    private static readonly Multiplier DefaultCap = new(10000.00m);

    [Fact(DisplayName = "Dados 100.000 cálculos, quando medidos, então a média é ≤ 1 ms")]
    [Trait("Spec", "SPEC-0007:UT-07")]
    public void GivenOneHundredThousandCalculationsWhenMeasuredThenAverageIsAtMostOneMillisecond()
    {
        const int Calculations = 100_000;
        var source = new DeterministicSeedSource("SPEC-0007:UT-07");
        var salt = new Salt(source.NextSeed());
        var seeds = Enumerable.Range(0, 1_000).Select(_ => new Seed(source.NextSeed())).ToArray();
        var rtp = new Rtp(0.97m);
        for (var i = 0; i < 1_000; i++)
        {
            CrashPointCalculator.Compute(seeds[i % seeds.Length], salt, rtp, DefaultCap);
        }

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < Calculations; i++)
        {
            CrashPointCalculator.Compute(seeds[i % seeds.Length], salt, rtp, DefaultCap);
        }

        stopwatch.Stop();

        var averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / Calculations;
        Assert.True(averageMilliseconds <= 1.0, $"Average of {averageMilliseconds:F4} ms per calculation, above 1 ms.");
    }
}
