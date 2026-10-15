using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wolverine;
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
    private static readonly string CashedOut = StatusName(BetStatus.CashedOut);
    private static readonly string Lost = StatusName(BetStatus.Lost);
    private static readonly string Refunded = StatusName(BetStatus.Refunded);
    private const string RefundPending = "pending";
    private const string RefundConfirmed = "refunded";
    private const string CreditPending = "pending";
    private const string Credited = "credited";

    /// <summary>Attempts of a compare-and-set of a bet whose row changes concurrently.</summary>
    private const int ConcurrencyAttempts = 5;

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

    public async Task<IReadOnlyList<PlayerBet>> ListPlayerBetsAsync(BetOwner owner, PlayerBetsCursor? before, int limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var bets = db.Bets.AsNoTracking().Where(b => b.OperatorId == owner.OperatorId.Value && b.PlayerId == owner.PlayerId.Value && b.Status != Pending);
        if (before is not null)
        {
            // Keyset on (created_at, id), the index ix_bets_player_history: one page costs the same at any depth.
            bets = bets.Where(b => EF.Functions.LessThan(ValueTuple.Create(b.CreatedAt, b.Id), ValueTuple.Create(before.PlacedAt, before.BetId)));
        }

        var records = await bets
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return [.. records.Select(r => new PlayerBet(ToDomain(r), r.CreatedAt))];
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

    public async Task<AcceptedBet?> FindByIdAsync(Guid betId, CancellationToken ct)
    {
        var record = await db.Bets.AsNoTracking().SingleOrDefaultAsync(b => b.Id == betId && b.Status != Pending, ct).ConfigureAwait(false);
        return record is null ? null : ToAccepted(record);
    }

    public async Task<IReadOnlyList<Bet>> ListAutoCashOutsAsync(RoundId roundId, CancellationToken ct)
    {
        var records = await db.Bets.AsNoTracking()
            .Where(b => b.RoundId == roundId.Value && b.Status == Active && b.AutoCashOut != null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return [.. records.Select(ToDomain)];
    }

    public async Task<bool> CashOutAsync(Bet bet, bool automatic, DateTimeOffset at, TimeSpan creditDelay, bool announce, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        if (bet is not { Status: BetStatus.CashedOut, CashOutMultiplier: { } multiplier, Payout: { } payout, RoundId: { } round })
        {
            throw new InvalidOperationException($"Bet {bet.Id} is not cashed out in a round.");
        }

        // A pending request recorded meanwhile changes the row's concurrency token, not whether the bet can be cashed out.
        for (var attempt = 0; attempt < ConcurrencyAttempts; attempt++)
        {
            if (await TryCashOutAsync(new CashOutChange(bet.Id, round.Value, multiplier, payout, automatic, at, creditDelay, announce), ct).ConfigureAwait(false) is { } done)
            {
                return done;
            }
        }

        return false;
    }

    /// <summary>One attempt of <see cref="CashOutAsync"/>: <see langword="null"/> when the row changed meanwhile (try again).</summary>
    private async Task<bool?> TryCashOutAsync(CashOutChange change, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            var record = await outbox.DbContext.Bets.SingleOrDefaultAsync(b => b.Id == change.BetId && b.Status == Active, ct).ConfigureAwait(false);
            if (record is null)
            {
                return false;
            }

            record.Status = CashedOut;
            record.CashOutMultiplier = change.Multiplier;
            record.Payout = change.Payout;
            record.CashOutAuto = change.Automatic;
            record.CashedOutAt = change.At;
            record.CreditStatus = CreditPending;

            // ADR-0007: the credit is stored with the cash-out itself.
            var credit = new CreditCashOut(record.Id, record.OperatorId, record.PlayerId, record.Currency, change.RoundId, change.Payout, Announce: change.Announce);
            await outbox.PublishAsync(credit, change.CreditDelay > TimeSpan.Zero ? new DeliveryOptions { ScheduleDelay = change.CreditDelay } : null)
                .ConfigureAwait(false);
            try
            {
                await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
        }
    }

    public async Task<bool> RequestCashOutAsync(Guid betId, DateTimeOffset receivedAt, TimeSpan decisionDelay, CancellationToken ct)
    {
        for (var attempt = 0; attempt < ConcurrencyAttempts; attempt++)
        {
            if (await TryRequestCashOutAsync(betId, receivedAt, decisionDelay, ct).ConfigureAwait(false) is { } pending)
            {
                return pending;
            }
        }

        return await db.Bets.AsNoTracking().AnyAsync(b => b.Id == betId && b.Status == Active && b.CashOutRequestedAt != null, ct).ConfigureAwait(false);
    }

    /// <summary>One attempt of <see cref="RequestCashOutAsync"/>: <see langword="null"/> when the row changed meanwhile (try again).</summary>
    private async Task<bool?> TryRequestCashOutAsync(Guid betId, DateTimeOffset receivedAt, TimeSpan decisionDelay, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            var record = await outbox.DbContext.Bets.SingleOrDefaultAsync(b => b.Id == betId && b.Status == Active, ct).ConfigureAwait(false);
            if (record is null)
            {
                return false;
            }

            if (record.CashOutRequestedAt is { } recorded && recorded <= receivedAt)
            {
                // The first reception stays.
                return true;
            }

            if (record.CashOutRequestedAt is null)
            {
                // One decision per request: the command is stored with its first record only.
                await outbox.PublishAsync(new DecideCashOut(betId), decisionDelay > TimeSpan.Zero ? new DeliveryOptions { ScheduleDelay = decisionDelay } : null)
                    .ConfigureAwait(false);
            }

            record.CashOutRequestedAt = receivedAt;
            try
            {
                await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
        }
    }

    public async Task<bool> ClearCashOutRequestAsync(Guid betId, CancellationToken ct) =>
        await db.Bets
            .Where(b => b.Id == betId && b.Status == Active && b.CashOutRequestedAt != null)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.CashOutRequestedAt, (DateTimeOffset?)null), ct)
            .ConfigureAwait(false) == 1;

    public async Task<bool> ConfirmCreditAsync(Guid betId, decimal balance, CancellationToken ct) =>
        await db.Bets
            .Where(b => b.Id == betId && b.Status == CashedOut && b.CreditStatus == CreditPending)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.CreditStatus, Credited).SetProperty(b => b.CreditBalance, balance), ct)
            .ConfigureAwait(false) == 1;

    public Task<string?> RoundOutcomeAsync(RoundId roundId, CancellationToken ct) =>
        db.RoundClosures.AsNoTracking().Where(c => c.RoundId == roundId.Value).Select(c => c.Outcome).SingleOrDefaultAsync(ct);

    public Task MarkRoundEndedAsync(RoundId roundId, string outcome, DateTimeOffset at, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO betting.round_closures (round_id, outcome, closed_at) VALUES ({roundId.Value}, {outcome}, {at}) ON CONFLICT (round_id) DO NOTHING",
            ct);

    public async Task<IReadOnlyList<Bet>> SettleLostAsync(RoundId roundId, CancellationToken ct)
    {
        // One statement for the whole round (5.000 bets in ≤ 2 s, SPEC-0024 IT-04); the rows it changed come back to notify.
        var lost = await db.Bets
            .FromSqlInterpolated($"UPDATE betting.bets SET status = {Lost} WHERE round_id = {roundId.Value} AND status = {Active} AND cash_out_requested_at IS NULL RETURNING *")
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return [.. lost.Select(ToDomain)];
    }

    public async Task<IReadOnlyList<Bet>> RefundRoundAsync(RoundId roundId, bool placedOnly, CancellationToken ct)
    {
        for (var attempt = 0; attempt < ConcurrencyAttempts; attempt++)
        {
            if (await TryRefundRoundAsync(roundId, placedOnly, ct).ConfigureAwait(false) is { } refunded)
            {
                return refunded;
            }
        }

        throw new InvalidOperationException($"The bets of annulled round {roundId.Value} kept changing; the annulment is retried.");
    }

    public async Task<bool> ConfirmRefundAsync(Guid betId, decimal refunded, decimal balance, CancellationToken ct) =>
        await db.Bets
            .Where(b => b.Id == betId && b.Status == Refunded && b.RefundStatus == RefundPending)
            .ExecuteUpdateAsync(
                set => set.SetProperty(b => b.RefundStatus, RefundConfirmed).SetProperty(b => b.Refunded, refunded).SetProperty(b => b.RefundBalance, balance),
                ct)
            .ConfigureAwait(false) == 1;

    public async Task<bool> CloseSettlementAsync(RoundId roundId, DateTimeOffset at, TimeSpan grace, CancellationToken ct)
    {
        var round = roundId.Value;
        var closure = await db.RoundClosures.AsNoTracking().SingleOrDefaultAsync(c => c.RoundId == round, ct).ConfigureAwait(false);
        if (closure is null
            || at < closure.ClosedAt + grace
            || await db.Bets.AsNoTracking().AnyAsync(b => b.RoundId == round && (b.Status == Placed || b.Status == Active), ct).ConfigureAwait(false))
        {
            return false;
        }

        // Closed bets are final: once none is open, the summary is the same whoever computes it. Every (operator, currency) row
        // of the round is written in one transaction; ON CONFLICT keeps the first one (immutable).
        var closed = await db.Bets.AsNoTracking()
            .Where(b => b.RoundId == round && (b.Status == Lost || b.Status == CashedOut || b.Status == Refunded))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var written = 0;
            foreach (var summary in RoundSettlementCalculator.Summarize(closed.Select(ToDomain)))
            {
                written += await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO betting.round_settlements (round_id, operator_id, currency, bets_count, total_wagered, total_paid, total_refunded, ggr, settled_at) VALUES ({round}, {summary.OperatorId.Value}, {summary.Currency.Code}, {summary.BetsCount}, {summary.TotalWagered}, {summary.TotalPaid}, {summary.TotalRefunded}, {summary.Ggr}, {at}) ON CONFLICT (round_id, operator_id, currency) DO NOTHING",
                    ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return written > 0;
        }
    }

    public async Task<bool> ReviveCashOutAsync(Bet bet, DateTimeOffset at, TimeSpan creditDelay, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        if (bet is not { Status: BetStatus.CashedOut, CashOutMultiplier: { } multiplier, Payout: { } payout, RoundId: { } round })
        {
            throw new InvalidOperationException($"Bet {bet.Id} is not cashed out in a round.");
        }

        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            var context = outbox.DbContext;
            if (await context.RoundSettlements.AsNoTracking().AnyAsync(s => s.RoundId == round.Value, ct).ConfigureAwait(false)
                || await context.Bets.SingleOrDefaultAsync(b => b.Id == bet.Id && b.Status == Lost, ct).ConfigureAwait(false) is not { } record)
            {
                return false;
            }

            record.Status = CashedOut;
            record.CashOutMultiplier = multiplier;
            record.Payout = payout;
            record.CashOutAuto = false;
            record.CashedOutAt = at;
            record.CreditStatus = CreditPending;
            await outbox.PublishAsync(
                new CreditCashOut(record.Id, record.OperatorId, record.PlayerId, record.Currency, round.Value, payout),
                new DeliveryOptions { ScheduleDelay = creditDelay }).ConfigureAwait(false);
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

    public async Task ScheduleDecisionAsync(Guid betId, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            await outbox.PublishAsync(new DecideCashOut(betId)).ConfigureAwait(false);
            await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>One attempt of <see cref="RefundRoundAsync"/>: <see langword="null"/> when a bet changed meanwhile (try again).</summary>
    private async Task<IReadOnlyList<Bet>?> TryRefundRoundAsync(RoundId roundId, bool placedOnly, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<BettingDbContext>>();
            var records = await outbox.DbContext.Bets
                .Where(b => b.RoundId == roundId.Value && (b.Status == Placed || (!placedOnly && b.Status == Active)))
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (records.Count == 0)
            {
                return [];
            }

            foreach (var record in records)
            {
                record.Status = Refunded;
                record.Refunded = record.Amount;
                record.RefundStatus = RefundPending;
                record.RefundBalance = null;
                record.CashOutRequestedAt = null;

                // ADR-0007: the refund is stored with the bet it refunds.
                await outbox.PublishAsync(new RefundBet(record.Id, record.OperatorId, record.PlayerId, record.Currency, roundId.Value, record.Amount)).ConfigureAwait(false);
            }

            try
            {
                await outbox.SaveChangesAndFlushMessagesAsync(ct).ConfigureAwait(false);
                return [.. records.Select(ToDomain)];
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
        }
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
            : null,
        record is { CashOutMultiplier: { } multiplier, Payout: { } payout, CreditStatus: { } credit }
            ? new CashOutRecord(multiplier, payout, record.CashOutAuto ?? false, string.Equals(credit, Credited, StringComparison.Ordinal), record.CreditBalance)
            : null,
        record.CashOutRequestedAt);

    private static Bet ToDomain(BetRecord record) => Bet.Restore(
        record.Id,
        new BetOwner(new OperatorId(record.OperatorId), new PlayerId(record.PlayerId), new Currency(record.Currency)),
        new BetRequest(record.ClientBetId, record.Panel, record.Amount, record.AutoCashOut),
        string.Equals(record.Status, Pending, StringComparison.Ordinal) ? ParseStatus(record.AcceptedStatus) : ParseStatus(record.Status),
        record.RoundId is { } round ? new RoundId(round) : null,
        record.RoundNumber,
        record.CashOutMultiplier,
        record.Payout,
        record.Refunded);

    private static string StatusName(BetStatus status) => status switch
    {
        BetStatus.Placed => "placed",
        BetStatus.Active => "active",
        BetStatus.Queued => "queued",
        BetStatus.Cancelled => "cancelled",
        BetStatus.CashedOut => "cashed_out",
        BetStatus.Lost => "lost",
        BetStatus.Refunded => "refunded",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown bet status."),
    };

    private static BetStatus ParseStatus(string status) => status switch
    {
        "placed" => BetStatus.Placed,
        "active" => BetStatus.Active,
        "queued" => BetStatus.Queued,
        "cancelled" => BetStatus.Cancelled,
        "cashed_out" => BetStatus.CashedOut,
        "lost" => BetStatus.Lost,
        "refunded" => BetStatus.Refunded,
        _ => throw new InvalidOperationException($"Unknown bet status '{status}' in the schema betting."),
    };

    [LoggerMessage(EventId = 2210, Level = LogLevel.Error, Message = "Queued bet {BetId} of room {SalaId} could not be activated; the room's other bets go on")]
    private static partial void ActivationFailed(ILogger logger, Guid betId, string salaId, Exception exception);

    /// <summary>The cash-out <see cref="CashOutAsync"/> stores, with how its credit runs.</summary>
    private sealed record CashOutChange(
        Guid BetId,
        Guid RoundId,
        decimal Multiplier,
        decimal Payout,
        bool Automatic,
        DateTimeOffset At,
        TimeSpan CreditDelay,
        bool Announce);
}
