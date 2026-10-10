using Apogeu.Betting.Domain;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary><see cref="IBetStore"/> on the schema <c>betting</c>; reads are not tracked.</summary>
internal sealed class BetStore(BettingDbContext db) : IBetStore
{
    private const string UniqueViolation = "23505";

    private static readonly string Placed = StatusName(BetStatus.Placed);
    private static readonly string Queued = StatusName(BetStatus.Queued);
    private static readonly string Active = StatusName(BetStatus.Active);

    public async Task<AcceptedBet?> FindByClientBetIdAsync(BetOwner owner, Guid clientBetId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var record = await Of(owner).SingleOrDefaultAsync(b => b.ClientBetId == clientBetId, ct).ConfigureAwait(false);
        return record is null ? null : ToAccepted(record);
    }

    public async Task<AcceptedBet?> FindAsync(BetOwner owner, Guid betId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var record = await Of(owner).SingleOrDefaultAsync(b => b.Id == betId, ct).ConfigureAwait(false);
        return record is null ? null : ToAccepted(record);
    }

    public async Task<IReadOnlyList<Bet>> ListOpenAsync(BetOwner owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var records = await Of(owner).Where(b => b.Status == Placed || b.Status == Queued).ToListAsync(ct).ConfigureAwait(false);
        return [.. records.Select(ToDomain)];
    }

    public async Task<IBetReservation> ReserveAsync(Bet bet, string salaId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        ArgumentNullException.ThrowIfNull(salaId);

        var record = new BetRecord
        {
            Id = bet.Id,
            OperatorId = bet.Owner.OperatorId.Value,
            PlayerId = bet.Owner.PlayerId.Value,
            Currency = bet.Owner.Currency.Code,
            SalaId = salaId,
            ClientBetId = bet.ClientBetId,
            Panel = (short)bet.Panel,
            Amount = bet.Amount,
            AutoCashOut = bet.AutoCashOut,
            Status = StatusName(bet.Status),
            RoundId = bet.RoundId?.Value,
            RoundNumber = bet.RoundNumber,
            AcceptedStatus = StatusName(bet.Status),
            AcceptedRoundId = bet.RoundId?.Value,
            AcceptedRoundNumber = bet.RoundNumber,
            CreatedAt = now,
        };

        var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            db.Bets.Add(record);

            // Blocks while a concurrent transaction holds the same clientBetId (or panel), until it commits or rolls back.
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new Reservation(db, transaction, record);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation } unique)
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            db.ChangeTracker.Clear();
            var conflict = string.Equals(unique.ConstraintName, BettingDbContext.ClientBetIdIndex, StringComparison.Ordinal)
                ? BetConflict.ClientBetId
                : BetConflict.Panel;
            throw new BetConflictException(conflict, exception);
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<bool> TransitionAsync(Bet bet, BetStatus expected, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        var from = StatusName(expected);
        var to = StatusName(bet.Status);
        var moved = await db.Bets
            .Where(b => b.Id == bet.Id && b.Status == from)
            .ExecuteUpdateAsync(
                set => set.SetProperty(b => b.Status, to)
                    .SetProperty(b => b.RoundId, bet.RoundId == null ? null : bet.RoundId.Value.Value)
                    .SetProperty(b => b.RoundNumber, bet.RoundNumber),
                ct)
            .ConfigureAwait(false);
        return moved == 1;
    }

    public Task<int> StartFlightAsync(RoundId roundId, CancellationToken ct) =>
        db.Bets
            .Where(b => b.RoundId == roundId.Value && b.Status == Placed)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.Status, Active), ct);

    public async Task<IReadOnlyList<Bet>> ActivateQueuedAsync(string salaId, RoundWindow opened, Guid? betId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opened);
        var query = db.Bets.AsNoTracking().Where(b => b.SalaId == salaId && b.Status == Queued);
        if (betId is { } only)
        {
            query = query.Where(b => b.Id == only);
        }

        var queued = await query.ToListAsync(ct).ConfigureAwait(false);
        var activated = new List<Bet>(queued.Count);
        foreach (var record in queued)
        {
            var bet = ToDomain(record);
            if (bet.Activate(opened) && await TransitionAsync(bet, BetStatus.Queued, ct).ConfigureAwait(false))
            {
                activated.Add(bet);
            }
        }

        return activated;
    }

    private IQueryable<BetRecord> Of(BetOwner owner) =>
        db.Bets.AsNoTracking().Where(b =>
            b.OperatorId == owner.OperatorId.Value && b.PlayerId == owner.PlayerId.Value && b.Currency == owner.Currency.Code);

    private static AcceptedBet ToAccepted(BetRecord record) => new(
        ToDomain(record),
        record.SalaId,
        ParseStatus(record.AcceptedStatus),
        record.AcceptedRoundId is { } round ? new RoundId(round) : null,
        record.AcceptedRoundNumber,
        record.BalanceAfter);

    private static Bet ToDomain(BetRecord record) => Bet.Restore(
        record.Id,
        new BetOwner(new OperatorId(record.OperatorId), new PlayerId(record.PlayerId), new Currency(record.Currency)),
        new BetRequest(record.ClientBetId, record.Panel, record.Amount, record.AutoCashOut),
        ParseStatus(record.Status),
        record.RoundId is { } round ? new RoundId(round) : null,
        record.RoundNumber);

    private static string StatusName(BetStatus status) => status switch
    {
        BetStatus.Placed => "placed",
        BetStatus.Active => "active",
        BetStatus.Queued => "queued",
        BetStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown bet status."),
    };

    private static BetStatus ParseStatus(string status) => status switch
    {
        "placed" => BetStatus.Placed,
        "active" => BetStatus.Active,
        "queued" => BetStatus.Queued,
        "cancelled" => BetStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown bet status '{status}' in the schema betting."),
    };

    /// <summary>The open transaction of a reserved bet: disposed without <see cref="CommitAsync"/>, it rolls back.</summary>
    private sealed class Reservation(BettingDbContext db, IDbContextTransaction transaction, BetRecord record) : IBetReservation
    {
        public async Task<AcceptedBet> CommitAsync(decimal balanceAfter, CancellationToken ct)
        {
            record.BalanceAfter = balanceAfter;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return ToAccepted(record);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }
    }
}
