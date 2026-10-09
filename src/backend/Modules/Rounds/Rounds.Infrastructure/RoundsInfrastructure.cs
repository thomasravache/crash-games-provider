using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Infrastructure;

/// <summary>
/// Composition of the Rounds persistence (SPEC-0008, ADR-0010): the schema <c>rounds</c> in PostgreSQL behind
/// <c>IRoundStore</c>, with its own DbContext and migrations.
/// </summary>
public static class RoundsInfrastructure
{
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>Registers <c>IRoundStore</c> backed by PostgreSQL, the connection string read when first needed.</summary>
    public static IServiceCollection AddRoundsInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<RoundsDbContext>((provider, options) => Configure(options, connectionString(provider)));
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

    /// <summary>PostgreSQL with the migrations history in the schema <c>rounds</c> itself.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, RoundsDbContext.Schema));
}
