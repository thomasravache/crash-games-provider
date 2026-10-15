using Apogeu.Fairness.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>
/// One process of the real Host in memory (WebApplicationFactory) in Development on real PostgreSQL and Redis, with the
/// Data Protection key ring in <c>DataProtection:KeysPath</c> (SPEC-0065). The Fairness hosted services stay, so the
/// schema <c>fairness</c> is migrated through the real path; the other modules' hosted services are removed (they would run
/// rounds of their own). Disposing it stops the process; a new one on the same database and directory is the restart.
/// </summary>
internal sealed class FairnessApiHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    private FairnessApiHost(WebApplicationFactory<Program> factory) => _factory = factory;

    public static async Task<FairnessApiHost> StartAsync(string database, string redisConnectionString, DirectoryInfo keysPath)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:apogeu", database);
            host.UseSetting("ConnectionStrings:redis", redisConnectionString);
            host.UseSetting("Operators:GameBaseUrl", "https://game.apogeu.test");
            host.UseSetting("DataProtection:KeysPath", keysPath.FullName);
            host.ConfigureTestServices(services =>
            {
                var otherModules = services
                    .Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationType?.Assembly.GetName().Name is { } assembly
                        && assembly.StartsWith("Apogeu.", StringComparison.Ordinal)
                        && !assembly.StartsWith("Apogeu.Fairness.", StringComparison.Ordinal))
                    .ToList();
                foreach (var descriptor in otherModules)
                {
                    services.Remove(descriptor);
                }
            });
        });

        try
        {
            // Starts the host now: the migrations of the schema fairness run here.
            _ = factory.Server;
        }
        catch
        {
            await factory.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new FairnessApiHost(factory);
    }

    /// <summary>Runs <paramref name="action"/> with the Host's <see cref="IFairnessService"/>, in a DI scope of its own.</summary>
    public async Task<T> RunAsync<T>(Func<IFairnessService, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var scope = _factory.Services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await action(scope.ServiceProvider.GetRequiredService<IFairnessService>()).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
