namespace Apogeu.Rounds.Tests.Support;

/// <summary>A span exported by the Host's OpenTelemetry pipeline.</summary>
internal sealed record ExportedSpan(string SourceName, string Name);
