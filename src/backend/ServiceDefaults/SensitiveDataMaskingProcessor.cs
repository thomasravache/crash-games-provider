using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Apogeu.ServiceDefaults;

/// <summary>
/// Log processor that replaces the values of sensitive keys (<c>serverSeed</c>, <c>token</c>, <c>secret</c>,
/// <c>signature</c>, <c>password</c>, <c>authorization</c>) with <c>***</c> before any exporter (ADR-0014, ADR-0017).
/// </summary>
public sealed class SensitiveDataMaskingProcessor : BaseProcessor<LogRecord>
{
    /// <summary>The value written in place of a sensitive value.</summary>
    public const string Mask = "***";

#pragma warning disable MA0025 // SPEC-0005 scaffold: the Implementer replaces this body.
    public override void OnEnd(LogRecord data) => throw new NotImplementedException();
#pragma warning restore MA0025
}
