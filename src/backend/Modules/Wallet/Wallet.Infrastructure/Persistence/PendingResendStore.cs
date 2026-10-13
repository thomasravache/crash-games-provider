using Microsoft.EntityFrameworkCore;

namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// The table <c>wallet.pending_resends</c> (SPEC-0046): credits and rollbacks of the seamless wallet not confirmed yet. An
/// attempt is taken by a lease (<c>next_attempt_at</c> moved past the attempt's deadline), so one row is never sent by two
/// attempts at once — by this process or another one after a restart.
/// </summary>
internal sealed class PendingResendStore(IDbContextFactory<WalletDbContext> contexts)
{
    /// <summary>
    /// Stores <paramref name="pending"/> (if new) and takes its attempt until <paramref name="leaseUntil"/>, returning the
    /// attempts already made; <see langword="null"/> when the row already exists with an attempt in flight or not due yet: the
    /// caller then does not send it.
    /// </summary>
    public async Task<int?> TryTakeAsync(PendingResendRecord pending, DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var db = await contexts.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var taken = await db.Database.SqlQuery<int>($"""
                INSERT INTO wallet.pending_resends (transaction_id, kind, operator_id, player_id, currency, amount, round_id, bet_id,
                    original_transaction_id, attempts, next_attempt_at, created_at)
                VALUES ({pending.TransactionId}, {pending.Kind}, {pending.OperatorId}, {pending.PlayerId}, {pending.Currency},
                    {pending.Amount}, {pending.RoundId}, {pending.BetId}, {pending.OriginalTransactionId}, 0, {leaseUntil}, {now})
                ON CONFLICT (transaction_id) DO UPDATE SET next_attempt_at = EXCLUDED.next_attempt_at
                WHERE wallet.pending_resends.next_attempt_at <= {now}
                RETURNING attempts AS "Value"
                """).ToListAsync(ct).ConfigureAwait(false);
            return taken.Count == 1 ? taken[0] : null;
        }
    }

    /// <summary>Takes up to <paramref name="max"/> rows due at <paramref name="now"/>, leased until <paramref name="leaseUntil"/>.</summary>
    public async Task<IReadOnlyList<PendingResendRecord>> TakeDueAsync(DateTimeOffset now, DateTimeOffset leaseUntil, int max, CancellationToken ct)
    {
        var db = await contexts.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return await db.PendingResends.FromSql($"""
                UPDATE wallet.pending_resends AS p SET next_attempt_at = {leaseUntil}
                WHERE p.transaction_id IN (
                    SELECT transaction_id FROM wallet.pending_resends
                    WHERE next_attempt_at <= {now}
                    ORDER BY next_attempt_at
                    LIMIT {max}
                    FOR UPDATE SKIP LOCKED)
                RETURNING p.*
                """).AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>The operation had a definitive answer: it is no longer pending.</summary>
    public async Task CompleteAsync(string transactionId, CancellationToken ct)
    {
        var db = await contexts.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlAsync(
                $"DELETE FROM wallet.pending_resends WHERE transaction_id = {transactionId}", ct).ConfigureAwait(false);
        }
    }

    /// <summary>The attempt was uncertain: <paramref name="attempts"/> made so far, the next one due at <paramref name="nextAttemptAt"/>.</summary>
    public async Task RescheduleAsync(string transactionId, int attempts, DateTimeOffset nextAttemptAt, CancellationToken ct)
    {
        var db = await contexts.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlAsync($"""
                UPDATE wallet.pending_resends SET attempts = {attempts}, next_attempt_at = {nextAttemptAt}
                WHERE transaction_id = {transactionId}
                """, ct).ConfigureAwait(false);
        }
    }
}
