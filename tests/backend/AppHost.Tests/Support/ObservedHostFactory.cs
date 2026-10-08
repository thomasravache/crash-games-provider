extern alias ApiHost;

using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using HostProgram = ApiHost::Program;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// The real Host (with its ServiceDefaults pipeline) in-process, exporting OTLP/HTTP to a <see cref="FakeOtlpCollector"/>.
/// Connection strings point to closed loopback ports: these tests observe telemetry, not the dependencies.
/// </summary>
internal static class ObservedHostFactory
{
    public static WebApplicationFactory<HostProgram> Create(
        Uri otlpEndpoint,
        Action<IServiceCollection>? configureTestServices = null) =>
        new WebApplicationFactory<HostProgram>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.UseSetting("ConnectionStrings:apogeu", "Host=127.0.0.1;Port=1;Database=apogeu;Username=apogeu;Timeout=1");
            host.UseSetting("ConnectionStrings:redis", "127.0.0.1:1,abortConnect=false,connectTimeout=200");

            host.UseSetting("OTEL_SERVICE_NAME", "api");
            host.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint.ToString().TrimEnd('/'));
            host.UseSetting("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            host.UseSetting("OTEL_BSP_SCHEDULE_DELAY", "100");
            host.UseSetting("OTEL_BLRP_SCHEDULE_DELAY", "100");
            host.UseSetting("OTEL_METRIC_EXPORT_INTERVAL", (60 * 60 * 1000).ToString(CultureInfo.InvariantCulture));

            // The request under observation must produce at least the ASP.NET Core request logs.
            host.UseSetting("Logging:LogLevel:Default", "Information");
            host.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Information");

            if (configureTestServices is not null)
            {
                host.ConfigureTestServices(configureTestServices);
            }
        });
}
