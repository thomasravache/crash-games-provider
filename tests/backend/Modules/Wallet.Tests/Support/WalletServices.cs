using Apogeu.Operators.Contracts;
using Apogeu.Security.Tests.Support;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// The Wallet module as the application composes it (<see cref="WalletInfrastructure.AddWalletInfrastructure"/>) on a
/// real PostgreSQL database of the test, with the migrations of the schema <c>wallet</c> applied and the limits of the
/// demo operator from the SPEC-0020@1 double (<see cref="FixedOperatorLimits"/>).
/// </summary>
internal sealed class WalletServices : IAsyncDisposable
{
    private WalletServices(ServiceProvider provider, string connectionString)
    {
        Provider = provider;
        ConnectionString = connectionString;
    }

    public ServiceProvider Provider { get; }

    /// <summary>The connection string the module itself uses (IT-06 runs its UPDATE/DELETE with it).</summary>
    public string ConnectionString { get; }

    /// <summary>A new database in the container, migrated through the module's own entry point.</summary>
    public static async Task<WalletServices> StartAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var database = await TestDatabase.CreateAsync(serverConnectionString, cancellationToken).ConfigureAwait(false);
        return await OpenAsync(database, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Another "process" of the module on an existing database (migrations are idempotent).</summary>
    public static async Task<WalletServices> OpenAsync(string databaseConnectionString, CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOperatorLimits, FixedOperatorLimits>();
        services.AddWalletInfrastructure(_ => databaseConnectionString);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        try
        {
            await provider.MigrateWalletDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new WalletServices(provider, databaseConnectionString);
    }

    /// <summary>Runs <paramref name="action"/> with the module's <c>IOperatorWallet</c>, in a DI scope of its own.</summary>
    public async Task<T> WithWalletAsync<T>(Func<IOperatorWallet, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var scope = Provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await action(scope.ServiceProvider.GetRequiredService<IOperatorWallet>()).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();
}
