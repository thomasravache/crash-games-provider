using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Infrastructure.Demo;
using Apogeu.Wallet.Infrastructure.Observability;
using Apogeu.Wallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Apogeu.Wallet.Infrastructure;

/// <summary>
/// Composition of the Wallet persistence and adapters (SPEC-0021, ADR-0007, ADR-0010): the insert-only ledger in the schema
/// <c>wallet</c> and the demo adapter of <see cref="IOperatorWallet"/> and <see cref="IDemoWallet"/>. Needs
/// <c>IOperatorLimits</c> (Operators module) and <see cref="TimeProvider"/> (Host) registered.
/// </summary>
public static class WalletInfrastructure
{
    /// <summary>
    /// Default <c>Maximum Pool Size</c> of the module's Npgsql pool (<c>Wallet:MaxPoolSize</c>): far below PostgreSQL's default
    /// <c>max_connections</c> (100) together with the other modules (SPEC-0032).
    /// </summary>
    public const int DefaultMaxPoolSize = 10;

    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>Registers the ledger and the adapters on the PostgreSQL connection string returned by the delegate (read when first used).</summary>
    public static IServiceCollection AddWalletInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> postgresConnectionString) =>
        services.AddWalletInfrastructure(postgresConnectionString, _ => DefaultMaxPoolSize);

    /// <summary>
    /// Registers the ledger and the adapters on a pool of their own bounded by <paramref name="maxPoolSize"/>: a burst of
    /// operations waits for a free connection (Npgsql <c>Timeout</c>) instead of opening more.
    /// </summary>
    public static IServiceCollection AddWalletInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> postgresConnectionString,
        Func<IServiceProvider, int> maxPoolSize)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(postgresConnectionString);
        ArgumentNullException.ThrowIfNull(maxPoolSize);

        services.AddPooledDbContextFactory<WalletDbContext>((provider, options) =>
            Configure(options, WithMaxPoolSize(postgresConnectionString(provider), maxPoolSize(provider))));
        services.AddMetrics();
        services.AddSingleton<WalletMetrics>();
        services.AddSingleton<IWalletLedger, PostgresWalletLedger>();
        services.AddSingleton<DemoWallet>();
        services.AddSingleton<IOperatorWallet>(provider => provider.GetRequiredService<DemoWallet>());
        services.AddSingleton<IDemoWallet>(provider => provider.GetRequiredService<DemoWallet>());

        return services;
    }

    /// <summary>Applies the migrations of the schema <c>wallet</c> (idempotent), including the insert-only triggers.</summary>
    public static async Task MigrateWalletDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var db = await services.GetRequiredService<IDbContextFactory<WalletDbContext>>().CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The connection string with its <c>Maximum Pool Size</c> (and <c>Minimum Pool Size</c> no larger): Npgsql keys its
    /// pools by connection string, so the module gets a pool of its own with that bound.
    /// </summary>
    internal static string WithMaxPoolSize(string connectionString, int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoolSize, 1);
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize };
        builder.MinPoolSize = Math.Min(builder.MinPoolSize, maxPoolSize);
        return builder.ConnectionString;
    }

    /// <summary>PostgreSQL with the migrations history in the schema <c>wallet</c> itself.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, WalletDbContext.Schema));
}
