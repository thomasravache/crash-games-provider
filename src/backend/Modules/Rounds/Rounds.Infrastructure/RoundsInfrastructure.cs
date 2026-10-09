#pragma warning disable MA0025 // Scaffold of SPEC-0008 (no logic yet): implemented in the green phase.

using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Infrastructure;

/// <summary>
/// Composition of the Rounds persistence (SPEC-0008, ADR-0010): the schema <c>rounds</c> in PostgreSQL behind
/// <c>IRoundStore</c>, with its own DbContext and migrations.
/// </summary>
public static class RoundsInfrastructure
{
    /// <summary>Registers <c>IRoundStore</c> backed by PostgreSQL, the connection string read when first needed.</summary>
    public static IServiceCollection AddRoundsInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> connectionString) =>
        throw new NotImplementedException();

    /// <summary>Applies the migrations of the schema <c>rounds</c> (idempotent).</summary>
    public static Task MigrateRoundsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
