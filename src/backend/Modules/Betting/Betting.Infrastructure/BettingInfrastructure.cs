using Apogeu.Betting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Apogeu.Betting.Infrastructure;

/// <summary>
/// Composition of the Betting persistence (SPEC-0022, ADR-0010): the schema <c>betting</c> in PostgreSQL behind
/// <see cref="IBetStore"/>, with its own DbContext, migrations and a bounded connection pool of its own.
/// </summary>
public static class BettingInfrastructure
{
    /// <summary>Default <c>Maximum Pool Size</c> of the module's Npgsql pool (as Rounds, IMP-02).</summary>
    public const int DefaultMaxPoolSize = 10;

    /// <summary>Application name of the module's connections: it also keys a pool of its own in Npgsql.</summary>
    public const string ApplicationName = "apogeu-betting";

    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Registers <see cref="IBetStore"/> on PostgreSQL with a pool of its own bounded by <paramref name="maxPoolSize"/>: a burst
    /// waits for a free connection instead of opening more.
    /// </summary>
    public static IServiceCollection AddBettingInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString,
        Func<IServiceProvider, int> maxPoolSize)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(maxPoolSize);

        services.AddDbContext<BettingDbContext>((provider, options) =>
            Configure(options, OwnPool(connectionString(provider), maxPoolSize(provider))));
        services.AddScoped<IBetStore, BetStore>();

        return services;
    }

    /// <summary>Applies the migrations of the schema <c>betting</c> (idempotent).</summary>
    public static async Task MigrateBettingDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<BettingDbContext>();
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The connection string with the module's application name and its <c>Maximum Pool Size</c> (and <c>Minimum Pool
    /// Size</c> no larger): Npgsql keys its pools by connection string, so the module gets a pool of its own with that bound.
    /// </summary>
    internal static string OwnPool(string connectionString, int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoolSize, 1);
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize, ApplicationName = ApplicationName };
        builder.MinPoolSize = Math.Min(builder.MinPoolSize, maxPoolSize);
        return builder.ConnectionString;
    }

    /// <summary>PostgreSQL with the migrations history in the schema <c>betting</c> itself.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, BettingDbContext.Schema));
}
