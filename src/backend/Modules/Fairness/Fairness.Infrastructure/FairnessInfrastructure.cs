using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Fairness.Infrastructure;

/// <summary>
/// Composition of the Fairness persistence (SPEC-0007, ADR-0010, ADR-0014): the schema <c>fairness</c> in PostgreSQL, the
/// chain origin and checkpoints encrypted with the host's Data Protection, and the <c>IFairnessService</c> implementation.
/// </summary>
public static class FairnessInfrastructure
{
    /// <summary>Name of the meter with <c>fairness.rounds_issued</c> and <c>fairness.chain_remaining</c>.</summary>
    public const string MeterName = FairnessMetrics.MeterName;

    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Registers <c>IFairnessService</c> backed by PostgreSQL at <paramref name="connectionString"/>. Uses the
    /// <c>IDataProtectionProvider</c> and logging configured by the host, without replacing their configuration.
    /// </summary>
    public static IServiceCollection AddFairnessInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddFairnessInfrastructure(_ => connectionString);
    }

    /// <summary>
    /// Same as <see cref="AddFairnessInfrastructure(IServiceCollection, string)"/>, with the connection string read when the
    /// DbContext is first created (for example from the host configuration).
    /// </summary>
    public static IServiceCollection AddFairnessInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionString);

        // Both only add their defaults when the host registered nothing (TryAdd): the host configuration wins.
        services.AddDataProtection();
        services.AddMetrics();

        services.AddDbContext<FairnessDbContext>((provider, options) => Configure(options, connectionString(provider)));
        services.AddSingleton<ChainLinkProtector>();
        services.AddSingleton<FairnessMetrics>();
        services.AddScoped<IFairnessService, FairnessService>();

        return services;
    }

    /// <summary>Applies the migrations of the schema <c>fairness</c> (idempotent).</summary>
    public static async Task MigrateFairnessDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<FairnessDbContext>();
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>PostgreSQL with the migrations history in the schema <c>fairness</c> itself.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, FairnessDbContext.Schema));
}
