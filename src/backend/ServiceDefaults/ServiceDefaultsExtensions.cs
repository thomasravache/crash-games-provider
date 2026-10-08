using System.Text.Json;
using Apogeu.ServiceDefaults.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Apogeu.ServiceDefaults;

/// <summary>
/// Service defaults every .NET service calls (ADR-0003, ADR-0017): OpenTelemetry (logs, metrics, traces, OTLP),
/// health checks, HttpClient resilience and masking of sensitive log fields.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Tag of the checks that only tell whether the process answers (<c>/alive</c>).</summary>
    public const string LiveTag = "live";

    /// <summary>Tag of the dependency checks (<c>/health</c> runs every check).</summary>
    public const string ReadyTag = "ready";

    private const string HealthPath = "/health";
    private const string AlivePath = "/alive";
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Registers OpenTelemetry, the default health checks and the HttpClient resilience defaults.</summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureStructuredConsoleLogs();
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        return builder;
    }

    /// <summary>Maps <c>/health</c> (all checks) and <c>/alive</c> (liveness only).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Both stay in the traces on purpose: a /health request is observable end to end like any other.
        app.MapHealthChecks(HealthPath, new HealthCheckOptions { ResponseWriter = WriteHealthReportAsync });
        app.MapHealthChecks(AlivePath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
            ResponseWriter = WriteHealthReportAsync,
        });

        return app;
    }

    /// <summary>
    /// JSON lines on the console with TraceId/SpanId, state and scopes, sensitive values masked
    /// (<see cref="MaskingJsonConsoleFormatter"/>).
    /// </summary>
    private static void ConfigureStructuredConsoleLogs(this IHostApplicationBuilder builder) =>
        builder.Logging
            .AddConsole(options => options.FormatterName = MaskingJsonConsoleFormatter.FormatterName)
            .AddConsoleFormatter<MaskingJsonConsoleFormatter, JsonConsoleFormatterOptions>(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "O";
            });

    private static void ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        // OTLP is on only when the environment points to a collector (the Aspire dashboard sets these variables);
        // endpoint, protocol and batching come from the standard OTEL_* settings.
        var useOtlp = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]);

        builder.Services.AddOpenTelemetry()
            .WithLogging(
                logging =>
                {
                    // Registered first: processors run in order, so nothing is exported before it is masked.
                    logging.AddProcessor(new SensitiveDataMaskingProcessor());
                    if (useOtlp)
                    {
                        logging.AddOtlpExporter();
                    }
                },
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (useOtlp)
                {
                    metrics.AddOtlpExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (useOtlp)
                {
                    tracing.AddOtlpExporter();
                }
            });
    }

    private static void AddDefaultHealthChecks(this IHostApplicationBuilder builder) =>
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

    /// <summary>
    /// <c>{"status":"Unhealthy","entries":{"postgres":{"status":"Unhealthy","description":"..."}}}</c>. Exception
    /// details are not written (they go to the logs).
    /// </summary>
    private static async Task WriteHealthReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var writer = new Utf8JsonWriter(context.Response.Body);
        await using var disposeWriter = writer.ConfigureAwait(false);
        writer.WriteStartObject();
        writer.WriteString("status", report.Status.ToString());
        writer.WriteNumber("totalDurationMs", Math.Round(report.TotalDuration.TotalMilliseconds, 1));
        writer.WriteStartObject("entries");
        foreach (var (name, entry) in report.Entries)
        {
            writer.WriteStartObject(name);
            writer.WriteString("status", entry.Status.ToString());
            if (entry.Description is { } description)
            {
                writer.WriteString("description", description);
            }

            writer.WriteNumber("durationMs", Math.Round(entry.Duration.TotalMilliseconds, 1));
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        await writer.FlushAsync(context.RequestAborted).ConfigureAwait(false);
    }
}
