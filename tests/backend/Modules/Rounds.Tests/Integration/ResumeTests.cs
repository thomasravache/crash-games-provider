using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FairnessMultiplier = Apogeu.Fairness.Contracts.Multiplier;

namespace Apogeu.Rounds.Tests.Integration;

[Collection(RoundsIntegration.Name)]
public sealed class ResumeTests(RoundsContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CrashTolerance = TimeSpan.FromMilliseconds(250);

    // Slower growth and a low cap: flights last up to ln(2)/0.00002 ≈ 34.7 s, long enough to outlive a takeover (≤ 6 s).
    private static readonly SalaSettings LongFlights = new(GrowthK: "0.00002", Cap: "2.00");

    [Fact(DisplayName = "Dado o líder derrubado no meio do voo, quando outra instância assume, então a rodada termina no mesmo crashPoint e em startedAt + t_c (± 250 ms)")]
    [Trait("Spec", "SPEC-0008:IT-04")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenLeaderKilledMidFlightWhenAnotherTakesOverThenRoundCrashesAtSamePointAndInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, LongFlights, ct);
        var hosts = new List<RoundsHost> { cluster.StartHost(), cluster.StartHost() };
        var samples = new List<string>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var leader = await Leaders.SingleAsync(hosts, TimeSpan.FromSeconds(30));
            var follower = hosts.Single(h => !ReferenceEquals(h, leader));
            await using var connection = follower.CreateHubConnection();
            var recorder = new HubRecorder(connection);
            await connection.StartAsync(ct);
            await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct);

            var flying = await recorder.WaitForAsync("RoundFlying", RoundTimeout);
            await leader.DisposeAsync();
            hosts.Remove(leader);
            await Eventually.HoldsAsync(() => follower.IsLeading, TimeSpan.FromSeconds(30), () => $"{follower.Name} taking over");
            var takeoverAt = DateTimeOffset.UtcNow;

            var crashed = await recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flying.RoundId, RoundTimeout);
            var crashPoint = Json.Decimal(crashed.Payload, "crashPoint");
            var startedAt = Json.Instant(flying.Payload, "startedAt");
            var expected = startedAt + TimeSpan.FromMilliseconds(Math.Log((double)crashPoint) / cluster.Sala.K);
            var crashedAt = Json.Instant(crashed.Payload, "crashedAt");
            samples.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"crash {crashPoint} expected {expected:O} crashedAt {crashedAt:O} takeover {takeoverAt:O}"));

            if (expected <= takeoverAt + TimeSpan.FromMilliseconds(500))
            {
                // The crash instant passed before the new leader could act: not a mid-flight takeover. Try the next round.
                hosts.Add(cluster.StartHost());
                continue;
            }

            Assert.True(
                (crashedAt - expected).Duration() <= CrashTolerance,
                $"The resumed round crashed at {crashedAt:O}, expected startedAt + t_c = {expected:O} (± {CrashTolerance.TotalMilliseconds} ms). Samples: {string.Join("; ", samples)}");
            Assert.Single(recorder.Of("RoundCrashed", flying.RoundId));

            using var http = follower.CreateClient();
            var history = await http.GetStringAsync(new Uri("/api/v1/salas/demo/rounds?limit=10", UriKind.Relative), ct);
            using var document = JsonDocument.Parse(history);
            var persisted = document.RootElement.EnumerateArray().Single(r => r.GetProperty("roundId").GetGuid() == flying.RoundId);
            Assert.Equal(crashPoint, Json.Decimal(persisted, "crashPoint"));
            return;
        }

        Assert.Fail($"No round outlived the takeover in 6 attempts. Samples: {string.Join("; ", samples)}");
    }

    // From the G4 review of SPEC-0008: the room's configuration changes between leader A and leader B while a round flies.
    // The round keeps the parameters it started with: the crash point A read (original RTP/cap) and t_c with the original k.
    [Fact(DisplayName = "Dada uma rodada em voo e a configuração da sala alterada entre o líder A e o líder B, quando B assume, então a rodada termina no crashPoint original e em startedAt + t_c com o growthK original (± 250 ms)")]
    [Trait("Spec", "SPEC-0008:IT-04")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenConfigurationChangedBetweenLeadersWhenRoundResumesThenItKeepsOriginalCrashPointAndInstant()
    {
        var ct = TestContext.Current.CancellationToken;
        var changed = new SalaSettings(Rtp: "0.92", Cap: "1.50", GrowthK: "0.00004");
        var samples = new List<string>();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await using var cluster = await RoundsCluster.CreateAsync(containers, LongFlights, ct);
            var leader = cluster.StartHost();
            await Eventually.HoldsAsync(() => leader.IsLeading, TimeSpan.FromSeconds(30), () => $"{leader.Name} leading");
            var successor = cluster.StartHost(new RoundsHostSetup
            {
                Salas = new Dictionary<string, SalaSettings>(StringComparer.Ordinal) { [SalaSettings.SalaId] = changed },
            });
            await using var connection = successor.CreateHubConnection();
            var recorder = new HubRecorder(connection);
            await connection.StartAsync(ct);
            await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct);

            var flying = await recorder.WaitForAsync("RoundFlying", RoundTimeout);
            await leader.DisposeAsync();
            await Eventually.HoldsAsync(() => successor.IsLeading, TimeSpan.FromSeconds(30), () => $"{successor.Name} taking over");
            var takeoverAt = DateTimeOffset.UtcNow;

            var crashed = await recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flying.RoundId, RoundTimeout);
            var expectedPoint = await OriginalCrashPointAsync(successor, crashed, ct);
            var startedAt = Json.Instant(flying.Payload, "startedAt");
            var expectedAt = startedAt + TimeSpan.FromMilliseconds(Math.Log((double)expectedPoint) / LongFlights.K);
            var crashPoint = Json.Decimal(crashed.Payload, "crashPoint");
            var crashedAt = Json.Instant(crashed.Payload, "crashedAt");
            samples.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"crash {crashPoint} (original {expectedPoint}) crashedAt {crashedAt:O} expected {expectedAt:O} takeover {takeoverAt:O}"));

            if (expectedAt <= takeoverAt + TimeSpan.FromMilliseconds(500))
            {
                // The original crash instant passed before the new leader could act: not a mid-flight takeover.
                continue;
            }

            Assert.True(
                crashPoint == expectedPoint,
                $"The resumed round crashed at {crashPoint}, expected its original crash point {expectedPoint} (RTP {LongFlights.Rtp}, cap {LongFlights.Cap}). Samples: {string.Join("; ", samples)}");
            Assert.True(
                (crashedAt - expectedAt).Duration() <= CrashTolerance,
                $"The resumed round crashed at {crashedAt:O}, expected startedAt + t_c with the original k = {expectedAt:O} (± {CrashTolerance.TotalMilliseconds} ms). Samples: {string.Join("; ", samples)}");
            return;
        }

        Assert.Fail($"No round outlived the takeover in 4 attempts. Samples: {string.Join("; ", samples)}");
    }

    [Fact(DisplayName = "Dado nenhum líder disponível por 31 s com rodada em voo, quando um líder assume, então a rodada fica Annulled com motivo e a métrica rounds.annulled incrementa")]
    [Trait("Spec", "SPEC-0008:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenNoLeaderForThirtyOneSecondsWithRoundInFlightWhenLeaderTakesOverThenRoundIsAnnulled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, LongFlights, ct);
        await using var store = new RoundStoreProbe(cluster.DatabaseConnectionString);
        var interrupted = await InterruptRoundInFlightAsync(cluster, store, ct);

        await Task.Delay(TimeSpan.FromSeconds(31), ct);

        using var listener = new AnnulledMeasurements();
        var successor = cluster.StartHost();
        var annulled = await Eventually.ResultAsync(
            () => store.FindAsync(interrupted, ct),
            r => r.State == RoundState.Annulled,
            TimeSpan.FromSeconds(20),
            () => $"round {interrupted} annulled by {successor.Name}");

        Assert.False(string.IsNullOrWhiteSpace(annulled.AnnulReason), "An annulled round records its reason.");
        Assert.Null(annulled.CrashPoint);
        await Eventually.HoldsAsync(
            () => listener.Total(successor.MeterFactory) >= 1,
            TimeSpan.FromSeconds(10),
            () => $"{RoundsMetrics.Annulled} incremented by {successor.Name} (got {listener.Total(successor.MeterFactory)})");
    }

    /// <summary>The crash point of the round's revealed seed under the parameters it started with (<see cref="LongFlights"/>).</summary>
    private static async Task<decimal> OriginalCrashPointAsync(RoundsHost host, HubEvent crashed, CancellationToken ct)
    {
        using var http = host.CreateClient();
        var verification = await Json.ReadAsync(
            await http.GetAsync(new Uri($"/api/v1/rounds/{crashed.RoundId}/verification", UriKind.Relative), ct).ConfigureAwait(false),
            ct).ConfigureAwait(false);
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IFairnessService>().ComputeCrashPoint(
            Seed.FromHex(Json.Text(crashed.Payload, "revealedSeed")),
            Salt.FromHex(Json.Text(verification, "salt")),
            new Rtp(decimal.Parse(LongFlights.Rtp, CultureInfo.InvariantCulture)),
            new FairnessMultiplier(decimal.Parse(LongFlights.Cap, CultureInfo.InvariantCulture))).Value;
    }

    /// <summary>Kills the only instance while a round flies, and returns that round (still Flying in the store).</summary>
    private static async Task<Guid> InterruptRoundInFlightAsync(RoundsCluster cluster, RoundStoreProbe store, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var host = cluster.StartHost();
            var connection = host.CreateHubConnection();
            await using var connectionDisposal = connection.ConfigureAwait(false);
            var recorder = new HubRecorder(connection);
            await connection.StartAsync(ct).ConfigureAwait(false);
            await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct).ConfigureAwait(false);

            var flying = await recorder.WaitForAsync("RoundFlying", RoundTimeout).ConfigureAwait(false);
            await connection.StopAsync(ct).ConfigureAwait(false);
            await host.DisposeAsync().ConfigureAwait(false);

            var round = await store.FindAsync(flying.RoundId, ct).ConfigureAwait(false);
            if (round?.State == RoundState.Flying)
            {
                return flying.RoundId;
            }
        }

        Assert.Fail("No round was still flying when its leader was killed, in 6 attempts.");
        return Guid.Empty;
    }

    /// <summary>Measurements of <c>rounds.annulled</c>, per meter factory (one per Host instance in this process).</summary>
    private sealed class AnnulledMeasurements : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly Lock _gate = new();
        private readonly List<(object? Scope, long Value)> _measurements = [];

        public AnnulledMeasurements()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, RoundsMetrics.MeterName, StringComparison.Ordinal)
                    && string.Equals(instrument.Name, RoundsMetrics.Annulled, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Add(instrument.Meter.Scope, value));
            _listener.SetMeasurementEventCallback<int>((instrument, value, _, _) => Add(instrument.Meter.Scope, value));
            _listener.Start();
        }

        public long Total(IMeterFactory factory)
        {
            lock (_gate)
            {
                return _measurements.Where(m => ReferenceEquals(m.Scope, factory)).Sum(m => m.Value);
            }
        }

        public void Dispose() => _listener.Dispose();

        private void Add(object? scope, long value)
        {
            lock (_gate)
            {
                _measurements.Add((scope, value));
            }
        }
    }
}
