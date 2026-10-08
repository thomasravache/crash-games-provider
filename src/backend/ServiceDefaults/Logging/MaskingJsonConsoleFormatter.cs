using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Apogeu.ServiceDefaults.Logging;

/// <summary>
/// Console formatter that writes one JSON object per log entry (ADR-0017): timestamp, level, category, message,
/// <c>TraceId</c>/<c>SpanId</c> of the current activity, the structured state and the scopes. Sensitive values are
/// masked in the message, the state and the scopes (ADR-0014), so the console never shows what the exporters hide.
/// </summary>
internal sealed class MaskingJsonConsoleFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "apogeu-json";

    private readonly IDisposable? _optionsReloadToken;
    private JsonConsoleFormatterOptions _options;

    public MaskingJsonConsoleFormatter(IOptionsMonitor<JsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
        _optionsReloadToken = options.OnChange(updated => _options = updated);
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var state = logEntry.State as IReadOnlyList<KeyValuePair<string, object?>>;
        var message = SensitiveData.MaskMessage(logEntry.Formatter(logEntry.State, logEntry.Exception), state);
        if (logEntry.Exception is null && message is null)
        {
            return;
        }

        var options = _options;
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer, options.JsonWriterOptions))
        {
            writer.WriteStartObject();
            if (options.TimestampFormat is { } timestampFormat)
            {
                var now = options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
                writer.WriteString("Timestamp", now.ToString(timestampFormat, CultureInfo.InvariantCulture));
            }

            writer.WriteNumber("EventId", logEntry.EventId.Id);
            writer.WriteString("LogLevel", logEntry.LogLevel.ToString());
            writer.WriteString("Category", logEntry.Category);
            writer.WriteString("Message", message);
            if (Activity.Current is { } activity)
            {
                writer.WriteString("TraceId", activity.TraceId.ToHexString());
                writer.WriteString("SpanId", activity.SpanId.ToHexString());
            }

            if (logEntry.Exception is { } exception)
            {
                writer.WriteString("Exception", exception.ToString());
            }

            if (state is not null)
            {
                writer.WriteStartObject("State");
                WriteProperties(writer, state);
                writer.WriteEndObject();
            }

            if (options.IncludeScopes && scopeProvider is not null)
            {
                WriteScopes(writer, scopeProvider);
            }

            writer.WriteEndObject();
        }

        textWriter.Write(Encoding.UTF8.GetString(buffer.WrittenSpan));
        textWriter.Write(Environment.NewLine);
    }

    public void Dispose() => _optionsReloadToken?.Dispose();

    private static void WriteScopes(Utf8JsonWriter writer, IExternalScopeProvider scopeProvider)
    {
        writer.WriteStartArray("Scopes");
        scopeProvider.ForEachScope(
            static (scope, jsonWriter) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> properties)
                {
                    jsonWriter.WriteStartObject();
                    WriteProperties(jsonWriter, [.. properties]);
                    jsonWriter.WriteEndObject();
                }
                else
                {
                    jsonWriter.WriteStringValue(ToInvariantString(scope));
                }
            },
            writer);
        writer.WriteEndArray();
    }

    private static void WriteProperties(Utf8JsonWriter writer, IReadOnlyList<KeyValuePair<string, object?>> properties)
    {
        foreach (var property in properties)
        {
            writer.WritePropertyName(property.Key);
            if (SensitiveData.IsSensitive(property.Key))
            {
                writer.WriteStringValue(SensitiveDataMaskingProcessor.Mask);
                continue;
            }

            WriteValue(writer, property.Value);
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case int or long or short or byte or sbyte or ushort or uint:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong unsigned:
                writer.WriteNumberValue(unsigned);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case double number when double.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            case float number when float.IsFinite(number):
                writer.WriteNumberValue(number);
                break;
            default:
                writer.WriteStringValue(ToInvariantString(value));
                break;
        }
    }

    private static string? ToInvariantString(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);
}
