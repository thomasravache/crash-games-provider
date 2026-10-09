using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Starts the real Host in memory (WebApplicationFactory) in a given environment. The connection strings point at
/// closed ports: the Host validates them at startup without connecting (SPEC-0005), and no test here needs the data stores.
/// The modules' hosted services are removed for the same reason: in Development they apply the module migrations at startup
/// (e.g. Fairness, SPEC-0007), which would try to reach the closed database port.
/// </summary>
internal static class ContractHost
{
    public const string OpenApiDocumentPath = "/openapi/v1.json";

    public static WebApplicationFactory<Program> Create(string environment, bool withFixtureEndpoint = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment(environment);
            host.UseSetting("ConnectionStrings:apogeu", "Host=127.0.0.1;Port=1;Database=apogeu;Username=apogeu;Timeout=1");
            host.UseSetting("ConnectionStrings:redis", "127.0.0.1:1,abortConnect=false,connectTimeout=200");
            host.ConfigureServices(services =>
            {
                var moduleHostedServices = services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationType?.Assembly.GetName().Name?.StartsWith("Apogeu.", StringComparison.Ordinal) == true)
                    .ToList();
                foreach (var descriptor in moduleHostedServices)
                {
                    services.Remove(descriptor);
                }
            });
            if (withFixtureEndpoint)
            {
                host.ConfigureServices(services => services.AddSingleton<IStartupFilter, FixtureEndpointStartupFilter>());
            }
        });
}
