using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Apogeu.ServiceDefaults.Tests.Support;

/// <summary>
/// Stands in for an exporter: registered after the processor under test, it snapshots the attributes each log
/// record carries when it reaches the end of the OpenTelemetry pipeline.
/// </summary>
internal sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
{
    private readonly Lock _gate = new();
    private readonly List<IReadOnlyList<KeyValuePair<string, object?>>> _records = [];

    public IReadOnlyList<IReadOnlyList<KeyValuePair<string, object?>>> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Log records are pooled by the SDK: copy the attributes instead of keeping the record.
        var attributes = data.Attributes?.ToList() ?? [];
        lock (_gate)
        {
            _records.Add(attributes);
        }
    }
}
