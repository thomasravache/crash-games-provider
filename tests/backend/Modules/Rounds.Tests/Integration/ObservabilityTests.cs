using System.Text.Json;
using Apogeu.Rounds.Features;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Rounds.Tests.Integration;

/// <summary>
/// The module metrics (among them the alert <c>rounds.annulled</c> of IT-05) and its traces reach the Host's OpenTelemetry
/// pipeline (SPEC-0008 §8, ADR-0017), not only an in-process listener (G4 review).
/// </summary>
[Collection(RoundsIntegration.Name)]
public sealed class ObservabilityTests(RoundsContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(15);

    [Fact(DisplayName = "Dado o Host real com o pipeline OpenTelemetry, quando uma rodada inicia o voo, então as métricas do medidor Apogeu.Rounds (rounds.started) são exportadas")]
    [Trait("Spec", "SPEC-0008:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostPipelineWhenRoundStartsThenRoundsMetricsAreExported()
    {
        var ct = TestContext.Current.CancellationToken;
        var capture = new OpenTelemetryCapture();
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var host = await StartFollowedRoundAsync(cluster, capture, ct);

        await Eventually.HoldsAsync(
            () =>
            {
                OpenTelemetryCapture.Flush(host.Services);
                return capture.Metrics.Any(m => string.Equals(m.MeterName, RoundsMetrics.MeterName, StringComparison.Ordinal)
                    && string.Equals(m.Name, RoundsMetrics.Started, StringComparison.Ordinal)
                    && m.Sum >= 1);
            },
            ExportTimeout,
            () => $"{RoundsMetrics.MeterName}/{RoundsMetrics.Started} exported by the Host's MeterProvider. {capture.Describe()}");
    }

    [Fact(DisplayName = "Dado o Host real com o pipeline OpenTelemetry, quando uma rodada é conduzida, então os spans da fonte Apogeu.Rounds são exportados")]
    [Trait("Spec", "SPEC-0008:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenRealHostPipelineWhenRoundIsConductedThenRoundsSpansAreExported()
    {
        var ct = TestContext.Current.CancellationToken;
        var capture = new OpenTelemetryCapture();
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var host = await StartFollowedRoundAsync(cluster, capture, ct);

        await Eventually.HoldsAsync(
            () =>
            {
                OpenTelemetryCapture.Flush(host.Services);
                return capture.Spans.Any(s => string.Equals(s.SourceName, RoundsMetrics.MeterName, StringComparison.Ordinal));
            },
            ExportTimeout,
            () => $"a span of the source {RoundsMetrics.MeterName} exported by the Host's TracerProvider. {capture.Describe()}");
    }

    /// <summary>Starts one instance with the capture and waits until a round of <c>demo</c> starts its flight.</summary>
    private static async Task<RoundsHost> StartFollowedRoundAsync(RoundsCluster cluster, OpenTelemetryCapture capture, CancellationToken ct)
    {
        var host = cluster.StartHost(new RoundsHostSetup { Services = capture.Register });
        var connection = host.CreateHubConnection();
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var recorder = new HubRecorder(connection);
        await connection.StartAsync(ct).ConfigureAwait(false);
        await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct).ConfigureAwait(false);
        await recorder.WaitForAsync("RoundFlying", RoundTimeout).ConfigureAwait(false);
        return host;
    }
}
