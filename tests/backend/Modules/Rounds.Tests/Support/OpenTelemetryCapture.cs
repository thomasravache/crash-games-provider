using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Adds an in-memory metric reader and span exporter to the OpenTelemetry providers the Host builds (ServiceDefaults,
/// ADR-0017), so a test sees exactly what the OTLP exporter would export: only the meters and sources the pipeline adds.
/// </summary>
internal sealed class OpenTelemetryCapture
{
    private readonly ConcurrentQueue<ExportedMetric> _metrics = new();
    private readonly ConcurrentQueue<ExportedSpan> _spans = new();

    public IReadOnlyList<ExportedMetric> Metrics => [.. _metrics];

    public IReadOnlyList<ExportedSpan> Spans => [.. _spans];

    public void Register(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddReader(new BaseExportingMetricReader(new MetricCollector(_metrics))));
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(new SpanCollector(_spans))));
    }

    /// <summary>Collects the metrics of the Host now (the reader only exports on demand).</summary>
    public static void Flush(IServiceProvider services)
    {
        services.GetRequiredService<MeterProvider>().ForceFlush();
        services.GetRequiredService<TracerProvider>().ForceFlush();
    }

    public string Describe() =>
        "Metrics: " + string.Join(", ", Metrics.Select(m => $"{m.MeterName}/{m.Name}={m.Sum}").Distinct(StringComparer.Ordinal))
        + Environment.NewLine + "Spans: " + string.Join(", ", Spans.Select(s => $"{s.SourceName}/{s.Name}").Distinct(StringComparer.Ordinal));

    private sealed class MetricCollector(ConcurrentQueue<ExportedMetric> metrics) : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                long sum = 0;
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    if (metric.MetricType is MetricType.LongSum or MetricType.LongSumNonMonotonic)
                    {
                        sum += point.GetSumLong();
                    }
                }

                metrics.Enqueue(new ExportedMetric(metric.MeterName, metric.Name, sum));
            }

            return ExportResult.Success;
        }
    }

    private sealed class SpanCollector(ConcurrentQueue<ExportedSpan> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                spans.Enqueue(new ExportedSpan(activity.Source.Name, activity.DisplayName));
            }

            return ExportResult.Success;
        }
    }
}
