using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Apogeu.ServiceDefaults;

/// <summary>
/// Service defaults every .NET service calls (ADR-0003, ADR-0017): OpenTelemetry (logs, metrics, traces, OTLP),
/// health checks, HttpClient resilience and masking of sensitive log fields.
/// </summary>
#pragma warning disable MA0025 // SPEC-0005 scaffold: the Implementer replaces these bodies.
public static class ServiceDefaultsExtensions
{
    /// <summary>Registers OpenTelemetry, the default health checks and the HttpClient resilience defaults.</summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        throw new NotImplementedException();

    /// <summary>Maps <c>/health</c> (all checks) and <c>/alive</c> (liveness only).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app) =>
        throw new NotImplementedException();
}
#pragma warning restore MA0025
