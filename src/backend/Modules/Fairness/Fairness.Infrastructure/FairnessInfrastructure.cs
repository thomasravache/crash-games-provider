using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Fairness.Infrastructure;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): implemented in the green phase.

/// <summary>
/// Composition of the Fairness persistence (SPEC-0007, ADR-0010, ADR-0014): the schema <c>fairness</c> in PostgreSQL, the
/// chain origin and checkpoints encrypted with the host's Data Protection, and the <c>IFairnessService</c> implementation.
/// </summary>
public static class FairnessInfrastructure
{
    /// <summary>
    /// Registers <c>IFairnessService</c> backed by PostgreSQL at <paramref name="connectionString"/>. Uses the
    /// <c>IDataProtectionProvider</c> and logging configured by the host, without replacing their configuration.
    /// </summary>
    public static IServiceCollection AddFairnessInfrastructure(this IServiceCollection services, string connectionString) =>
        throw new NotImplementedException();

    /// <summary>Applies the migrations of the schema <c>fairness</c> (idempotent).</summary>
    public static Task MigrateFairnessDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
#pragma warning restore MA0025
