using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

#pragma warning disable MA0025 // Scaffold of SPEC-0020 (no logic yet): implemented in the green phase.

/// <summary>
/// The idempotent seed of SPEC-0020 §2: the game <c>pipa</c>, enabled for the demo operator, and the demo operator's
/// default BRL limits (scaffold).
/// </summary>
public static class CatalogSeed
{
    /// <summary>Writes the seed through <paramref name="store"/>; running it again changes nothing.</summary>
    public static Task SeedAsync(ICatalogStore store, CancellationToken cancellationToken) => throw new NotImplementedException();
}
#pragma warning restore MA0025
