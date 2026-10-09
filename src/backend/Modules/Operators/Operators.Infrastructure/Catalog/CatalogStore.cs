using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Operators.Infrastructure.Catalog;

/// <summary>
/// EF Core implementation of <see cref="ICatalogStore"/> on the schema <c>operators</c> (SPEC-0020). It is a singleton, so
/// the in-memory caches of the catalog and the limits share its <see cref="Version"/>: each operation runs on an
/// <see cref="OperatorsDbContext"/> of a DI scope of its own, and every write moves the version once it is saved.
/// </summary>
/// <remarks>
/// The version lives in the process: the catalog is written only by the seed and by this instance (no backoffice edits yet,
/// SPEC-0020 §2), so a write made by another instance is seen after a restart.
/// </remarks>
internal sealed class CatalogStore(IServiceScopeFactory scopes) : ICatalogStore
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public CatalogSnapshot Load()
    {
        // The version is read before the rows: a write saved meanwhile moves it again, and the caches reload once more.
        var version = Version;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();

        var games = db.Games.AsNoTracking().OrderBy(g => g.Code).ToList();
        var enablements = db.OperatorGames.AsNoTracking()
            .Where(e => e.Enabled)
            .OrderBy(e => e.OperatorId).ThenBy(e => e.GameCode)
            .ToList();
        var limits = db.OperatorLimits.AsNoTracking()
            .OrderBy(l => l.OperatorId).ThenBy(l => l.Currency)
            .ToList();

        return new CatalogSnapshot(
            version,
            [.. games.Select(ToDefinition)],
            [.. enablements.Select(e => new GameEnablement(new OperatorId(e.OperatorId), new GameCode(e.GameCode)))],
            [.. limits.Select(ToLimits)]);
    }

    public async Task EnsureGameAsync(GameDefinition game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);

        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();
            var record = await db.Games.SingleOrDefaultAsync(g => g.Code == game.Code.Value, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                record = new GameRecord { Code = game.Code.Value, Name = game.Name, ClientApp = game.ClientApp, Theme = game.Theme };
                db.Games.Add(record);
            }

            record.Name = game.Name;
            record.ClientApp = game.ClientApp;
            record.Theme = game.Theme;
            record.Rtp = game.DefaultSala.Rtp;
            record.Cap = game.DefaultSala.Cap.Value;
            record.GrowthK = game.DefaultSala.GrowthK;
            record.BettingSeconds = game.DefaultSala.BettingSeconds;
            record.PauseSeconds = game.DefaultSala.PauseSeconds;
            record.Currencies = [.. game.Currencies.Select(c => c.Code)];

            await SaveAsync(db, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SetGameEnabledAsync(OperatorId op, GameCode code, bool enabled, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();
            var record = await db.OperatorGames
                .SingleOrDefaultAsync(e => e.OperatorId == op.Value && e.GameCode == code.Value, cancellationToken)
                .ConfigureAwait(false);
            if (record is null)
            {
                db.OperatorGames.Add(new OperatorGameRecord { OperatorId = op.Value, GameCode = code.Value, Enabled = enabled });
            }
            else
            {
                record.Enabled = enabled;
            }

            await SaveAsync(db, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SetLimitsAsync(OperatorId op, BetLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<OperatorsDbContext>();
            var record = await db.OperatorLimits
                .SingleOrDefaultAsync(l => l.OperatorId == op.Value && l.Currency == limits.Currency.Code, cancellationToken)
                .ConfigureAwait(false);
            if (record is null)
            {
                record = new OperatorLimitsRecord { OperatorId = op.Value, Currency = limits.Currency.Code };
                db.OperatorLimits.Add(record);
            }

            record.MinBet = limits.MinBet.Value;
            record.MaxBet = limits.MaxBet.Value;
            record.MaxWin = limits.MaxWin.Value;

            await SaveAsync(db, cancellationToken).ConfigureAwait(false);
        }
    }

    private static GameDefinition ToDefinition(GameRecord game) =>
        new(
            new GameCode(game.Code),
            game.Name,
            game.ClientApp,
            game.Theme,
            new SalaDefaults(game.Rtp, new Multiplier(game.Cap), game.GrowthK, game.BettingSeconds, game.PauseSeconds),
            [.. game.Currencies.Select(code => new Currency(code))]);

    private static OperatorCurrencyLimits ToLimits(OperatorLimitsRecord limits) =>
        new(
            new OperatorId(limits.OperatorId),
            new BetLimits(new Currency(limits.Currency), new Money(limits.MinBet), new Money(limits.MaxBet), new Money(limits.MaxWin)));

    private async Task SaveAsync(OperatorsDbContext db, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _version);
    }
}
