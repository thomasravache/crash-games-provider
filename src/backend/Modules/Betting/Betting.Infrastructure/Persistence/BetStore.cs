using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wolverine.EntityFrameworkCore;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary><see cref="IBetStore"/> on the schema <c>betting</c>; reads are not tracked.</summary>
internal sealed partial class BetStore(BettingDbContext db, IServiceScopeFactory scopes, ILogger<BetStore> logger) : IBetStore
{
    private const string UniqueViolation = "23505";

    private static readonly string Pending = "pending";
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
        var record = await Of(owner).SingleOrDefaultAsync(b => b.Id == betId && b.Status != Pending, ct).ConfigureAwait(false);
        return record is null ? null : ToAccepted(record);
    }

    public async Task<IReadOnlyList<Bet>> ListOpenAsync(BetOwner owner, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        // A pending reservation holds its panel like the bet it would become (UT-06, second review of emenda 3).
        var records = await Of(owner).Where(b => b.Status == Placed || b.Status == Queued || b.Status == Pending).ToListAsync(ct).ConfigureAwait(false);
        return [.. records.Select(ToDomain)];
    }

    public async Task ReserveAsync(Bet bet, string salaId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        ArgumentNullException.ThrowIfNull(salaId);

        db.Bets.Add(new BetRecord
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
            Status = Pending,
            RoundId = bet.RoundId?.Value,
            RoundNumber = bet.RoundNumber,
            AcceptedStatus = StatusName(bet.Status),
            AcceptedRoundId = bet.RoundId?.Value,
            AcceptedRoundNumber = bet.RoundNumber,
            CreatedAt = now,
        });
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation } unique)
        {
            throw new BetConflictException(
                string.Equals(unique.ConstraintName, BettingDbContext.ClientBetIdIndex, StringComparison.Ordinal) ? BetConflict.ClientBetId : BetConflict.Panel,
                exception);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<BetConfirmation> ConfirmAsync(Guid betId, decimal balanceAfter, CancellationToken ct)
    {
        try
        {
            var confirmed = await db.Bets
                .Where(b => b.Id == betId && b.Status == Pending)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(b => b.Status, b => b.AcceptedStatus).SetProperty(b => b.BalanceAfter, balanceAfter),
                    ct)
                .ConfigureAwait(false);
            return confirmed == 1 ? BetConfirmation.Confirmed : BetConfirmation.Voided;
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            return BetConfirmation.PanelTaken;
        }
    }

    public Task ReleaseAsync(Guid betId, CancellationToken ct) =>
        db.Bets.Where(b => b.Id == betId && b.Status == Pending).ExecuteDeleteAsync(ct);

    public async Task<bool> VoidAsync(Guid betId, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            var record = await outbox.DbContext.Bets.SingleOrDefaultAsync(b => b.Id == betId && b.Status == Pending, ct).ConfigureAwait(false);
            if (record is null)
            {
                return false;
            }

            // The concurrency token is the status: a reservation confirmed meanwhile is not voided.
            outbox.DbContext.Bets.Remove(record);
            await outbox.PublishAsync(Reversal(record)).ConfigureAwait(false);
            try
            {
                await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }
    }

    public async Task ReverseLateDebitAsync(Bet bet, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            await outbox.PublishAsync(new ReverseBetDebit(
                bet.Id,
                bet.Owner.OperatorId.Value,
                bet.Owner.PlayerId.Value,
                bet.Owner.Currency.Code,
                bet.RoundId?.Value)).ConfigureAwait(false);
            await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<Guid>> ListStalePendingAsync(DateTimeOffset createdBefore, int limit, CancellationToken ct) =>
        await db.Bets.AsNoTracking()
            .Where(b => b.Status == Pending && b.CreatedAt < createdBefore)
            .OrderBy(b => b.CreatedAt)
            .Select(b => b.Id)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public Task<bool> TransitionAsync(Bet bet, BetStatus expected, CancellationToken ct) => TransitionAsync(bet, expected, refund: null, ct);

    public async Task<bool> TransitionAsync(Bet bet, BetStatus expected, BetRefund? refund, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            var from = StatusName(expected);
            var record = await outbox.DbContext.Bets.SingleOrDefaultAsync(b => b.Id == bet.Id && b.Status == from, ct).ConfigureAwait(false);
            if (record is null)
            {
                return false;
            }

            record.Status = StatusName(bet.Status);
            record.RoundId = bet.RoundId?.Value;
            record.RoundNumber = bet.RoundNumber;
            if (refund is not null)
            {
                record.Refunded = refund.Refunded;
                record.RefundStatus = refund.Confirmed ? "refunded" : "pending";
                record.RefundBalance = refund.Balance;
            }

            if (bet.Status == BetStatus.Cancelled && expected != BetStatus.Cancelled)
            {
                // ADR-0007: the refund of a cancelled bet is stored with the cancellation itself.
                await outbox.PublishAsync(Reversal(record)).ConfigureAwait(false);
            }

            try
            {
                await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }
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
            try
            {
                if (bet.Activate(opened) && await ActivateAsync(bet, ct).ConfigureAwait(false))
                {
                    activated.Add(bet);
                }
            }
            catch (Exception exception) when (IsUniqueViolation(exception) || exception is InvalidOperationException)
            {
                // One bet that cannot be activated (its panel is taken in the round) does not stop the room's other bets
                // (emenda 3). Any other database error fails the message, so the activation is retried.
                ActivationFailed(logger, bet.Id, salaId, exception);
            }
        }

        return activated;
    }

    private async Task<bool> ActivateAsync(Bet bet, CancellationToken ct) =>
        await db.Bets
            .Where(b => b.Id == bet.Id && b.Status == Queued)
            .ExecuteUpdateAsync(
                set => set.SetProperty(b => b.Status, Placed)
                    .SetProperty(b => b.RoundId, bet.RoundId!.Value.Value)
                    .SetProperty(b => b.RoundNumber, bet.RoundNumber),
                ct)
            .ConfigureAwait(false) == 1;

    private static bool IsUniqueViolation(Exception exception) =>
        exception is PostgresException { SqlState: UniqueViolation } || exception.InnerException is PostgresException { SqlState: UniqueViolation };

    private IQueryable<BetRecord> Of(BetOwner owner) =>
        db.Bets.AsNoTracking().Where(b =>
            b.OperatorId == owner.OperatorId.Value && b.PlayerId == owner.PlayerId.Value && b.Currency == owner.Currency.Code);

    private static ReverseBetDebit Reversal(BetRecord record) =>
        new(record.Id, record.OperatorId, record.PlayerId, record.Currency, record.RoundId);

    private static AcceptedBet ToAccepted(BetRecord record) => new(
        ToDomain(record),
        record.SalaId,
        ParseStatus(record.AcceptedStatus),
        record.AcceptedRoundId is { } round ? new RoundId(round) : null,
        record.AcceptedRoundNumber,
        record.BalanceAfter,
        string.Equals(record.Status, Pending, StringComparison.Ordinal),
        record is { Refunded: { } refunded, RefundStatus: { } status }
            ? new BetRefund(refunded, string.Equals(status, "refunded", StringComparison.Ordinal), record.RefundBalance)
            : null);

    private static Bet ToDomain(BetRecord record) => Bet.Restore(
        record.Id,
        new BetOwner(new OperatorId(record.OperatorId), new PlayerId(record.PlayerId), new Currency(record.Currency)),
        new BetRequest(record.ClientBetId, record.Panel, record.Amount, record.AutoCashOut),
        string.Equals(record.Status, Pending, StringComparison.Ordinal) ? ParseStatus(record.AcceptedStatus) : ParseStatus(record.Status),
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

    [LoggerMessage(EventId = 2210, Level = LogLevel.Error, Message = "Queued bet {BetId} of room {SalaId} could not be activated; the room's other bets go on")]
    private static partial void ActivationFailed(ILogger logger, Guid betId, string salaId, Exception exception);
}
