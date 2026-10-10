using Apogeu.Operators.Contracts;

namespace Apogeu.Operators.Infrastructure.Catalog;

/// <summary>What <see cref="ICatalogStore.Load"/> returns: the stored catalog at one <see cref="Version"/>.</summary>
/// <param name="Version">The <see cref="ICatalogStore.Version"/> this snapshot reflects.</param>
/// <param name="Games">Every game of the catalog.</param>
/// <param name="Enablements">The games enabled for each operator (only enabled pairs).</param>
/// <param name="Limits">The bet limits of each operator, one entry per operator and currency.</param>
public sealed record CatalogSnapshot(
    long Version,
    IReadOnlyList<GameDefinition> Games,
    IReadOnlyList<GameEnablement> Enablements,
    IReadOnlyList<OperatorCurrencyLimits> Limits);
