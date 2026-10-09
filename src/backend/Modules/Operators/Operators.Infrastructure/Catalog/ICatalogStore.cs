using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;

namespace Apogeu.Operators.Infrastructure.Catalog;

/// <summary>
/// The persisted game catalog, the games enabled per operator and the bet limits per operator and currency (SPEC-0020),
/// in the schema <c>operators</c>. Every write is idempotent and changes <see cref="Version"/>, which the in-memory caches
/// of the catalog and the limits use to know when to reload.
/// </summary>
public interface ICatalogStore
{
    /// <summary>Version of the stored catalog and limits: it changes whenever a write changes them.</summary>
    long Version { get; }

    /// <summary>Everything stored: the games, the enabled games per operator and the limits per operator and currency.</summary>
    CatalogSnapshot Load();

    /// <summary>Registers the game, or updates it when it already exists (idempotent).</summary>
    Task EnsureGameAsync(GameDefinition game, CancellationToken cancellationToken);

    /// <summary>Enables or disables the game <paramref name="code"/> for the operator <paramref name="op"/> (idempotent).</summary>
    Task SetGameEnabledAsync(OperatorId op, GameCode code, bool enabled, CancellationToken cancellationToken);

    /// <summary>Sets the limits of <paramref name="op"/> in <c>limits.Currency</c>, replacing the previous ones (idempotent).</summary>
    Task SetLimitsAsync(OperatorId op, BetLimits limits, CancellationToken cancellationToken);
}
