using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Integration;

[Collection(ConsoleOutputGroup.Name)]
public sealed partial class LogMaskingTests
{
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Dado um endpoint de teste que registra token=abc, quando chamado, então nem a saída de log nem o exportador OTLP contêm abc")]
    [Trait("Spec", "SPEC-0005:IT-04")]
    public async Task GivenEndpointLoggingTokenWhenCalledThenNeitherLogOutputNorOtlpExportContainTheValue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var collector = await FakeOtlpCollector.StartAsync(cancellationToken);
        using var console = new ConsoleCapture();
        var probe = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        var probeBytes = Encoding.UTF8.GetBytes(probe);
        bool exported, written;
        await using (var factory = ObservedHostFactory.Create(
            collector.Endpoint,
            services => TokenLoggingEndpoint.Register(services, probe)))
        {
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(new Uri(TokenLoggingEndpoint.Path, UriKind.Relative), cancellationToken);
            Assert.True(response.IsSuccessStatusCode, $"Setup failed: the test endpoint returned {(int)response.StatusCode}.");

            // Controls: the log record reaches both the log output and the OTLP exporter (observed while the Host is
            // alive), so the absence checks below are meaningful.
            exported = await FakeOtlpCollector.EventuallyAsync(
                () => collector.Contains("logs", probeBytes), ExportTimeout, cancellationToken);
            written = await FakeOtlpCollector.EventuallyAsync(
                () => console.Text.Contains(probe, StringComparison.Ordinal), ExportTimeout, cancellationToken);
        }

        var output = console.Text;
        Assert.True(
            exported,
            $"The test log record was not exported over OTLP ({collector.Payloads("logs").Count} log payload(s) received).");
        Assert.True(
            written,
            $"The test log record did not reach the log output. Output:{Environment.NewLine}{Tail(output)}");

        Assert.False(
            StandaloneValue().IsMatch(output),
            $"The log output contains the token value '{TokenLoggingEndpoint.TokenValue}'. Output:{Environment.NewLine}{Tail(output)}");
        foreach (var signal in new[] { "logs", "traces" })
        {
            Assert.False(
                collector.Payloads(signal).Any(LeaksTokenValue),
                $"The OTLP {signal} export contains the token value '{TokenLoggingEndpoint.TokenValue}'.");
        }
    }

    /// <summary>
    /// "abc" as a value of its own: not part of a longer hexadecimal run (TraceId/SpanId rendered as text).
    /// </summary>
    [GeneratedRegex("(?<![0-9A-Fa-f])abc(?![0-9A-Fa-f])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex StandaloneValue();

    /// <summary>
    /// In OTLP protobuf the value leaks as a length-prefixed string (0x03 "abc", e.g. an attribute value), inside the
    /// formatted message ("token=abc") or inside a JSON-rendered body ("abc"). Raw ids are binary, so a bare 3-byte
    /// match is not used.
    /// </summary>
    private static bool LeaksTokenValue(byte[] payload)
    {
        var span = payload.AsSpan();
        return span.IndexOf([(byte)0x03, (byte)'a', (byte)'b', (byte)'c']) >= 0
            || span.IndexOf("token=abc"u8) >= 0
            || span.IndexOf("\"abc\""u8) >= 0;
    }

    private static string Tail(string text) => text.Length <= 4000 ? text : "[...]" + text[^4000..];
}
