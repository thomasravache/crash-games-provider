using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

/// <summary><see cref="IGameCatalog"/> over the <see cref="ICatalogStore"/>, cached in memory by version (SPEC-0020 §6).</summary>
public sealed class GameCatalog : IGameCatalog
{
    private readonly CatalogCache<View> _cache;

    public GameCatalog(ICatalogStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _cache = new CatalogCache<View>(store, View.Of);
    }

    public GameDefinition? Find(GameCode code) => _cache.Current.Games.GetValueOrDefault(code);

    public bool IsEnabledFor(OperatorId op, GameCode code)
    {
        var view = _cache.Current;
        return view.Games.ContainsKey(code) && view.Enabled.Contains(new GameEnablement(op, code));
    }

    /// <summary>The games the operator <paramref name="op"/> may launch, ordered by code.</summary>
    public IReadOnlyList<GameDefinition> EnabledFor(OperatorId op)
    {
        var view = _cache.Current;
        return [.. view.Games.Values
            .Where(game => view.Enabled.Contains(new GameEnablement(op, game.Code)))
            .OrderBy(game => game.Code.Value, StringComparer.Ordinal)];
    }

    private sealed record View(IReadOnlyDictionary<GameCode, GameDefinition> Games, IReadOnlySet<GameEnablement> Enabled)
    {
        public static View Of(CatalogSnapshot snapshot) =>
            new(snapshot.Games.ToDictionary(game => game.Code), snapshot.Enablements.ToHashSet());
    }
}
