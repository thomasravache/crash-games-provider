using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Apogeu.Rounds.Infrastructure;

/// <summary>
/// Composition of the Rounds persistence (SPEC-0008, ADR-0010): the schema <c>rounds</c> in PostgreSQL behind
/// <c>IRoundStore</c>, with its own DbContext and migrations.
/// </summary>
public static class RoundsInfrastructure
{
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Default <c>Maximum Pool Size</c> of the module's Npgsql pool: the conductor, the shared snapshot reads and the REST
    /// reads of an instance fit in it, far below PostgreSQL's default <c>max_connections</c> (100) together with the other
    /// modules (IMP-02).
    /// </summary>
    public const int DefaultMaxPoolSize = 10;

    /// <summary>Registers <c>IRoundStore</c> backed by PostgreSQL, the connection string read when first needed.</summary>
    public static IServiceCollection AddRoundsInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString) =>
        services.AddRoundsInfrastructure(connectionString, _ => DefaultMaxPoolSize);

    /// <summary>
    /// Registers <c>IRoundStore</c> backed by PostgreSQL on a pool of its own bounded by <paramref name="maxPoolSize"/>: a burst
    /// of reads waits for a free connection (Npgsql <c>Timeout</c>) instead of opening more. EF Core's retry on transient
    /// failures stays off, so a saturated server is never hit again by retries of the same burst.
    /// </summary>
    public static IServiceCollection AddRoundsInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString,
        Func<IServiceProvider, int> maxPoolSize)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(maxPoolSize);

        services.AddDbContext<RoundsDbContext>((provider, options) =>
            Configure(options, WithMaxPoolSize(connectionString(provider), maxPoolSize(provider))));
        services.AddScoped<RoundStore>();
        services.AddScoped<IRoundStore>(provider => provider.GetRequiredService<RoundStore>());
        services.AddScoped<IRoundChainStore>(provider => provider.GetRequiredService<RoundStore>());

        return services;
    }

    /// <summary>Applies the migrations of the schema <c>rounds</c> (idempotent).</summary>
    public static async Task MigrateRoundsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<RoundsDbContext>();
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

    /// <summary>PostgreSQL with the migrations history in the schema <c>rounds</c> itself.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, RoundsDbContext.Schema));
}
