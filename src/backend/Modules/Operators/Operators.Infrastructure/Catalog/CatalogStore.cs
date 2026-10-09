using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Persistence;

namespace Apogeu.Operators.Infrastructure.Catalog;

#pragma warning disable MA0025 // Scaffold of SPEC-0020 (no logic yet): implemented in the green phase.

/// <summary>EF Core implementation of <see cref="ICatalogStore"/> on the schema <c>operators</c> (SPEC-0020; scaffold).</summary>
internal sealed class CatalogStore : ICatalogStore
{
    public CatalogStore(OperatorsDbContext db) => ArgumentNullException.ThrowIfNull(db);

    public long Version => throw new NotImplementedException();

    public CatalogSnapshot Load() => throw new NotImplementedException();

    public Task EnsureGameAsync(GameDefinition game, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task SetGameEnabledAsync(OperatorId op, GameCode code, bool enabled, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task SetLimitsAsync(OperatorId op, BetLimits limits, CancellationToken cancellationToken) => throw new NotImplementedException();
}
#pragma warning restore MA0025
