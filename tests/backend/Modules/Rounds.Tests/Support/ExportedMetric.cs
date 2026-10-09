namespace Apogeu.Rounds.Tests.Support;

/// <summary>A metric exported by the Host's OpenTelemetry pipeline: meter, instrument and the sum of its long points.</summary>
internal sealed record ExportedMetric(string MeterName, string Name, long Sum);
