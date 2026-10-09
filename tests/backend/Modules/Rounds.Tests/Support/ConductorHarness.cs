using System.Diagnostics.Metrics;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.TestKit.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// One <see cref="SalaConductor"/> on a <see cref="FakeTimeProvider"/> with an in-memory store, a recording broadcaster and
/// a Fairness double. <see cref="RunUntilAsync"/> drives it deterministically: step, then jump the clock to the instant the
/// conductor asked for.
/// </summary>
internal sealed class ConductorHarness : IDisposable
{
    private readonly ServiceProvider _metricsProvider;

    public ConductorHarness(Func<TimeProvider, IFairnessService> fairness, SalaConfig? sala = null)
    {
        Clock = TestClock.Create();
        Store = new InMemoryRoundStore();
        Broadcaster = new RecordingBroadcaster(Clock);
        Fairness = fairness(Clock);
        Sala = sala ?? DemoSala();
        _metricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        Conductor = new SalaConductor(
            Sala,
            Fairness,
            Store,
            Broadcaster,
            new RoundsMetrics(_metricsProvider.GetRequiredService<IMeterFactory>()),
            Clock,
            NullLogger<SalaConductor>.Instance);
    }

    public FakeTimeProvider Clock { get; }

    public InMemoryRoundStore Store { get; }

    public RecordingBroadcaster Broadcaster { get; }

    public IFairnessService Fairness { get; }

    public SalaConfig Sala { get; }

    public SalaConductor Conductor { get; }

    /// <summary>The demo room of SPEC-0008 §4: RTP 0.97, cap 10000.00, k = 0.00006/ms, bets 6 s, pause 3 s.</summary>
    public static SalaConfig DemoSala() =>
        new("demo", 0.97m, 10000.00m, GrowthCurve.DefaultK, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(3), ChainLength: 100);

    /// <summary>Steps the conductor until <paramref name="done"/> holds, moving the clock to each instant it returns.</summary>
    public async Task RunUntilAsync(Func<bool> done, string what, int maxSteps = 20_000)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var step = 0; step < maxSteps; step++)
        {
            if (done())
            {
                return;
            }

            var next = await Conductor.StepAsync(ct).ConfigureAwait(false);
            if (next > Clock.GetUtcNow())
            {
                Clock.SetUtcNow(next);
            }
        }

        if (!done())
        {
            Assert.Fail($"After {maxSteps} steps, still waiting for {what}. Messages:{Environment.NewLine}{Broadcaster.Describe()}");
        }
    }

    public void Dispose() => _metricsProvider.Dispose();
}
