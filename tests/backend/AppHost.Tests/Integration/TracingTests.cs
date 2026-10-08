using System.Diagnostics;
using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Integration;

[Collection(ConsoleOutputGroup.Name)]
public sealed class TracingTests
{
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Dada uma requisição a /health, quando concluída, então existe um trace exportado e os logs da requisição carregam o mesmo TraceId")]
    [Trait("Spec", "SPEC-0005:IT-03")]
    public async Task GivenHealthRequestWhenCompletedThenTraceIsExportedAndRequestLogsCarryTheSameTraceId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var collector = await FakeOtlpCollector.StartAsync(cancellationToken);
        using var console = new ConsoleCapture();

        // The request joins a known trace (W3C traceparent, sampled), so its TraceId is known to the test.
        var traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();

        var traceIdBytes = Convert.FromHexString(traceId.ToHexString());
        bool traceExported, logExported, logOutputCarriesTraceId;
        await using (var factory = ObservedHostFactory.Create(collector.Endpoint))
        {
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
            request.Headers.Add("traceparent", $"00-{traceId.ToHexString()}-{parentSpanId.ToHexString()}-01");
            using var response = await client.SendAsync(request, cancellationToken);

            // Exporters batch in the background: observe while the Host is alive (its shutdown flush is not relied on).
            // OTLP carries the TraceId as its 16 raw bytes; the log output renders it as hexadecimal text.
            traceExported = await FakeOtlpCollector.EventuallyAsync(
                () => collector.Contains("traces", traceIdBytes), ExportTimeout, cancellationToken);
            logExported = await FakeOtlpCollector.EventuallyAsync(
                () => collector.Contains("logs", traceIdBytes), ExportTimeout, cancellationToken);
            logOutputCarriesTraceId = await FakeOtlpCollector.EventuallyAsync(
                () => console.Text.Contains(traceId.ToHexString(), StringComparison.OrdinalIgnoreCase), ExportTimeout, cancellationToken);
        }

        Assert.True(
            traceExported,
            $"No span with TraceId {traceId} was exported over OTLP ({collector.Payloads("traces").Count} trace payload(s) received).");
        Assert.True(
            logExported,
            $"No log exported over OTLP carries TraceId {traceId} ({collector.Payloads("logs").Count} log payload(s) received).");
        Assert.True(
            logOutputCarriesTraceId,
            $"The structured log output of the request does not carry TraceId {traceId}. Output:{Environment.NewLine}{Tail(console.Text)}");
    }

    private static string Tail(string text) => text.Length <= 4000 ? text : "[...]" + text[^4000..];
}
