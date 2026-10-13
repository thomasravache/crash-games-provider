using Apogeu.Operators.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Infrastructure.Demo;
using Apogeu.Wallet.Infrastructure.Observability;
using Apogeu.Wallet.Infrastructure.Persistence;
using Apogeu.Wallet.Infrastructure.Seamless;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apogeu.Wallet.Infrastructure;

/// <summary>
/// Composition of the Wallet persistence and adapters (SPEC-0021, ADR-0007, ADR-0010): the insert-only ledger in the schema
/// <c>wallet</c>, the demo adapter (<see cref="IDemoWallet"/>) and the seamless one (SPEC-0046), chosen per operator by the
/// <see cref="IOperatorWallet"/> of the module. Needs
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

    /// <summary>Service key of the HTTP client of the operator wallets (SPEC-0046).</summary>
    private const string SeamlessHttpClient = "Apogeu.Wallet.Seamless";

    /// <summary>Largest answer read from an operator wallet (its contract answers are a few hundred bytes).</summary>
    private const int MaxOperatorAnswerBytes = 64 * 1024;

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

        // Scoped too, from the same pool: the Wolverine outbox of the module (IDbContextOutbox, ADR-0002/0018) works on it.
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<WalletDbContext>>().CreateDbContext());
        services.AddMetrics();
        services.AddSingleton<WalletMetrics>();
        services.AddSingleton<IWalletLedger, PostgresWalletLedger>();
        services.AddSingleton<DemoWallet>();
        services.AddSingleton<IDemoWallet>(provider => provider.GetRequiredService<DemoWallet>());
        AddSeamlessWallet(services);

        return services;
    }

    /// <summary>
    /// The seamless wallet (SPEC-0046): the <see cref="IOperatorWallet"/> that picks demo or seamless per operator, the
    /// provisional configuration of the operators (unless one is registered already), the HTTP client of the operator wallets
    /// (deadlines enforced by the adapter, not by <see cref="HttpClient.Timeout"/>; redirects not followed; answers bounded)
    /// and the durable resend (scheduled Wolverine messages through the outbox, handled by the Wallet.Features handler).
    /// </summary>
    private static void AddSeamlessWallet(IServiceCollection services)
    {
        services.TryAddSingleton<IOperatorWalletSettings>(provider =>
            new ConfigurationOperatorWalletSettings(provider.GetService<IConfiguration>()));
        services.AddKeyedSingleton(SeamlessHttpClient, (_, _) => new HttpClient(
            new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = SeamlessOperatorWallet.AttemptDeadline,

                // A redirect of the operator is uncertain, never followed (the signed request is not sent elsewhere).
                AllowAutoRedirect = false,
            })
        {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = MaxOperatorAnswerBytes,
        });
        services.AddSingleton(provider => new SeamlessOperatorWallet(
            provider.GetRequiredKeyedService<HttpClient>(SeamlessHttpClient),
            provider.GetRequiredService<IOperatorWalletSettings>(),
            provider.GetRequiredService<IWalletLedger>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SeamlessOperatorWallet>>(),
            provider.GetRequiredService<WalletMetrics>()));
        services.AddSingleton(provider => new SeamlessResends(
            provider.GetRequiredService<SeamlessOperatorWallet>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<SeamlessResends>>(),
            provider.GetRequiredService<WalletMetrics>()));
        services.AddSingleton<OperatorWalletRouter>();
        services.AddSingleton<IOperatorWallet>(provider => provider.GetRequiredService<OperatorWalletRouter>());
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
