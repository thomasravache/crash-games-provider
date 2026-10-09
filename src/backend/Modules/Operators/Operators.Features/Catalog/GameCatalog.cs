using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

#pragma warning disable MA0025 // Scaffold of SPEC-0020 (no logic yet): implemented in the green phase.

/// <summary><see cref="IGameCatalog"/> over the <see cref="ICatalogStore"/>, cached in memory by version (SPEC-0020; scaffold).</summary>
public sealed class GameCatalog : IGameCatalog
{
    public GameCatalog(ICatalogStore store) => ArgumentNullException.ThrowIfNull(store);

    public GameDefinition? Find(GameCode code) => throw new NotImplementedException();

    public bool IsEnabledFor(OperatorId op, GameCode code) => throw new NotImplementedException();
}
#pragma warning restore MA0025
