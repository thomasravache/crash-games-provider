using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Starts the real Host in memory (WebApplicationFactory) in a given environment. The connection strings point at
/// closed ports: the Host validates them at startup without connecting (SPEC-0005), and no test here needs the data stores.
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
            if (withFixtureEndpoint)
            {
                host.ConfigureServices(services => services.AddSingleton<IStartupFilter, FixtureEndpointStartupFilter>());
            }
        });
}
