using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Infrastructure.Demo;
using Apogeu.Wallet.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Wallet.Infrastructure;

#pragma warning disable MA0025 // Scaffold of SPEC-0021 (no logic yet): implemented in the green phase.

/// <summary>
/// Composition of the Wallet persistence and adapters (SPEC-0021, ADR-0007, ADR-0010): the insert-only ledger in the schema
/// <c>wallet</c> and the demo adapter of <see cref="IOperatorWallet"/> and <see cref="IDemoWallet"/>. Needs
/// <c>IOperatorLimits</c> (Operators module) and <see cref="TimeProvider"/> (Host) registered.
/// </summary>
public static class WalletInfrastructure
{
    /// <summary>Registers the ledger and the adapters on the PostgreSQL connection string returned by the delegate (read when first used).</summary>
    public static IServiceCollection AddWalletInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> postgresConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(postgresConnectionString);

        services.AddSingleton<IWalletLedger>(provider => new PostgresWalletLedger(() => postgresConnectionString(provider)));
        services.AddSingleton<DemoWallet>();
        services.AddSingleton<IOperatorWallet>(provider => provider.GetRequiredService<DemoWallet>());
        services.AddSingleton<IDemoWallet>(provider => provider.GetRequiredService<DemoWallet>());

        return services;
    }

    /// <summary>Applies the migrations of the schema <c>wallet</c> (idempotent), including the insert-only triggers.</summary>
    public static Task MigrateWalletDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
#pragma warning restore MA0025
