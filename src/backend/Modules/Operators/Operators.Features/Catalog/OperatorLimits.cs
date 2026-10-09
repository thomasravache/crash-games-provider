using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

#pragma warning disable MA0025 // Scaffold of SPEC-0020 (no logic yet): implemented in the green phase.

/// <summary>
/// <see cref="IOperatorLimits"/> over the <see cref="ICatalogStore"/>, with an in-memory cache invalidated when the store
/// version changes (SPEC-0020 §5: a read in at most 1 ms; scaffold).
/// </summary>
public sealed class OperatorLimits : IOperatorLimits
{
    public OperatorLimits(ICatalogStore store) => ArgumentNullException.ThrowIfNull(store);

    public BetLimits Get(OperatorId op, Currency currency) => throw new NotImplementedException();
}
#pragma warning restore MA0025
