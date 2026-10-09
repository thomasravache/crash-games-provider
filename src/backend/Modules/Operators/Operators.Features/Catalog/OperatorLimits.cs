using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

/// <summary>
/// <see cref="IOperatorLimits"/> over the <see cref="ICatalogStore"/>, with an in-memory cache invalidated when the store
/// version changes (SPEC-0020 §5: a read in at most 1 ms).
/// </summary>
public sealed class OperatorLimits : IOperatorLimits
{
    private readonly CatalogCache<IReadOnlyDictionary<(OperatorId Operator, Currency Currency), BetLimits>> _cache;

    public OperatorLimits(ICatalogStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _cache = new CatalogCache<IReadOnlyDictionary<(OperatorId Operator, Currency Currency), BetLimits>>(
            store,
            snapshot => snapshot.Limits.ToDictionary(entry => (entry.Operator, entry.Limits.Currency), entry => entry.Limits));
    }

    public BetLimits Get(OperatorId op, Currency currency) =>
        _cache.Current.TryGetValue((op, currency), out var limits)
            ? limits
            : throw new CurrencyNotEnabledException(op, currency);
}
