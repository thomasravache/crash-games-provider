using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Tests.Support;

/// <summary>
/// Test double of the persistence boundary (<see cref="ICatalogStore"/>) for the unit tests of SPEC-0020: the catalog in
/// memory, a <see cref="Version"/> that changes on every write and a count of <see cref="Load"/> calls, so a test can see
/// whether the reader caches the catalog.
/// </summary>
internal sealed class InMemoryCatalogStore : ICatalogStore
{
    private readonly Dictionary<string, GameDefinition> _games = new(StringComparer.Ordinal);
    private readonly HashSet<GameEnablement> _enablements = [];
    private readonly Dictionary<(string Operator, string Currency), OperatorCurrencyLimits> _limits = [];
    private long _version;
    private int _loads;

    public long Version => Interlocked.Read(ref _version);

    /// <summary>How many times <see cref="Load"/> was called.</summary>
    public int LoadCount => Volatile.Read(ref _loads);

    public CatalogSnapshot Load()
    {
        Interlocked.Increment(ref _loads);
        return new CatalogSnapshot(Version, [.. _games.Values], [.. _enablements], [.. _limits.Values]);
    }

    public Task EnsureGameAsync(GameDefinition game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        _games[game.Code.Value] = game;
        Interlocked.Increment(ref _version);
        return Task.CompletedTask;
    }

    public Task SetGameEnabledAsync(OperatorId op, GameCode code, bool enabled, CancellationToken cancellationToken)
    {
        var pair = new GameEnablement(op, code);
        if (enabled)
        {
            _enablements.Add(pair);
        }
        else
        {
            _enablements.Remove(pair);
        }

        Interlocked.Increment(ref _version);
        return Task.CompletedTask;
    }

    public Task SetLimitsAsync(OperatorId op, BetLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits[(op.Value, limits.Currency.Code)] = new OperatorCurrencyLimits(op, limits);
        Interlocked.Increment(ref _version);
        return Task.CompletedTask;
    }
}
