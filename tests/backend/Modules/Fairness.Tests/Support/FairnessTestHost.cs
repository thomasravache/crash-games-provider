using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Infrastructure;
using Apogeu.TestKit.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>
/// One "process" of the Fairness module for the integration tests: a service provider with the real PostgreSQL, Data
/// Protection keys persisted in a directory (so a second host, the restarted process, reads what the first one wrote) and
/// optionally a <see cref="LogCapture"/>. Each call runs in its own DI scope, whatever the lifetime of the service.
/// </summary>
internal sealed class FairnessTestHost : IAsyncDisposable
{
    private const string ApplicationName = "Apogeu.Fairness.Tests";
    private readonly ServiceProvider _provider;

    private FairnessTestHost(ServiceProvider provider) => _provider = provider;

    public static async Task<FairnessTestHost> StartAsync(
        string connectionString,
        DirectoryInfo keyDirectory,
        LogCapture? logCapture,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            if (logCapture is not null)
            {
                logging.AddProvider(logCapture);
            }
        });
        services.AddDataProtection().SetApplicationName(ApplicationName).PersistKeysToFileSystem(keyDirectory);
        services.AddFairnessInfrastructure(connectionString);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        try
        {
            await provider.MigrateFairnessDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new FairnessTestHost(provider);
    }

    public async Task<T> RunAsync<T>(Func<IFairnessService, Task<T>> action)
    {
        var scope = _provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await action(scope.ServiceProvider.GetRequiredService<IFairnessService>()).ConfigureAwait(false);
        }
    }

    public T Run<T>(Func<IFairnessService, T> action)
    {
        using var scope = _provider.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<IFairnessService>());
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
