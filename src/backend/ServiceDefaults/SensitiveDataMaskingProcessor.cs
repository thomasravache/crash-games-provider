using Apogeu.ServiceDefaults.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Apogeu.ServiceDefaults;

/// <summary>
/// Log processor that replaces the values of sensitive keys (<c>serverSeed</c>, <c>token</c>, <c>secret</c>,
/// <c>signature</c>, <c>password</c>, <c>authorization</c>) with <c>***</c> before any exporter (ADR-0014, ADR-0017).
/// It must be registered before the exporters: processors run in registration order.
/// </summary>
public sealed class SensitiveDataMaskingProcessor : BaseProcessor<LogRecord>
{
    /// <summary>The value written in place of a sensitive value.</summary>
    public const string Mask = "***";

    /// <summary>Masks the attributes and the formatted message/body of <paramref name="data"/> in place.</summary>
    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Attributes is not { } attributes || !SensitiveData.HasSensitiveKey(attributes))
        {
            return;
        }

        data.FormattedMessage = SensitiveData.MaskMessage(data.FormattedMessage, attributes);

        // The body is the message template when there is one (it carries no values); otherwise it may be the
        // formatted message itself.
        if (data.Body is { } body && !string.Equals(body, SensitiveData.FindTemplate(attributes), StringComparison.Ordinal))
        {
            data.Body = SensitiveData.MaskMessage(body, attributes);
        }

        data.Attributes = SensitiveData.MaskAttributes(attributes);
    }
}
