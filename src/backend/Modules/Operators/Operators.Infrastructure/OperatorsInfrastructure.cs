using Apogeu.Operators.Infrastructure.Persistence;
using Apogeu.Operators.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Operators.Infrastructure;

/// <summary>
/// Composition of the Operators persistence (SPEC-0009, ADR-0010): the schema <c>operators</c> in PostgreSQL (operators and
/// launch grants) and Redis for replay detection and rate limits.
/// </summary>
public static class OperatorsInfrastructure
{
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Registers the stores on the PostgreSQL and Redis connection strings returned by the delegates, read when first used
    /// (for example from the host configuration).
    /// </summary>
    public static IServiceCollection AddOperatorsInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> postgresConnectionString,
        Func<IServiceProvider, string> redisConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(postgresConnectionString);
        ArgumentNullException.ThrowIfNull(redisConnectionString);

        services.AddDbContext<OperatorsDbContext>((provider, options) => Configure(options, postgresConnectionString(provider)));
        services.AddScoped<IOperatorDirectory, OperatorDirectory>();
        services.AddScoped<ILaunchGrantStore, LaunchGrantStore>();
        services.AddSingleton(provider => new OperatorsRedis(() => redisConnectionString(provider)));
        services.AddSingleton<IReplayGuard, RedisReplayGuard>();
        services.AddSingleton<IRateLimiter, RedisSlidingWindowRateLimiter>();

        return services;
    }

    /// <summary>Applies the migrations of the schema <c>operators</c> (idempotent).</summary>
    public static async Task MigrateOperatorsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>PostgreSQL with the migrations history in the schema <c>operators</c> itself.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, OperatorsDbContext.Schema));
}
