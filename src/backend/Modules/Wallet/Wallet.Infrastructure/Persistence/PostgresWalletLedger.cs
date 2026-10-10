using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// The ledger in the schema <c>wallet</c> of PostgreSQL (ADR-0010), insert-only (a <c>BEFORE UPDATE OR DELETE</c> trigger
/// refuses any change). It is a singleton: each operation runs on a <see cref="WalletDbContext"/> of its own, rented from the
/// module's context pool (the debit is on the 100 ms bet path, ADR-0019).
/// </summary>
/// <remarks>
/// <see cref="TryAppendAsync"/> calls the function <c>wallet.try_append_entry</c> (migration <c>LedgerAppendFunction</c>) in one
/// round trip: it serializes the appends of one player account with a transaction-scoped advisory lock, then checks the
/// transaction id and the expected sum before inserting the entry and its postings; the primary key on the transaction id
/// is the last guard of idempotency (a same id for another account). Reads go through EF Core.
/// </remarks>
internal sealed class PostgresWalletLedger(IDbContextFactory<WalletDbContext> contexts) : IWalletLedger
{
    public async Task<LedgerEntry?> FindAsync(TransactionId transactionId, CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var record = await db.Entries.AsNoTracking()
                .Include(e => e.Postings)
                .SingleOrDefaultAsync(e => e.TransactionId == transactionId.Value, cancellationToken)
                .ConfigureAwait(false);
            return record is null ? null : ToEntry(record);
        }
    }

    public async Task<Money> SumAsync(LedgerAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            return new Money(await SumOfAsync(db, account, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<LedgerAppendOutcome> TryAppendAsync(LedgerEntry entry, Money expectedPlayerSum, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Postings.Any(p => p.Account.Operator != entry.Player.OperatorId || p.Account.Currency != entry.Currency))
        {
            throw new ArgumentException("Every posting of an entry is on the operator and currency of the entry.", nameof(entry));
        }

        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            // One statement, one round trip: lock of the player account, checks and inserts in wallet.try_append_entry.
            var outcome = await db.Database.SqlQuery<string>($"""
                SELECT wallet.try_append_entry(
                    {Text(entry.TransactionId.Value)}, {Text(entry.Kind.ToString())}, {Text(entry.Player.OperatorId.Value)},
                    {Text(entry.Player.PlayerId.Value)}, {Text(entry.Currency.Code)}, {Uuid(entry.RoundId?.Value)}, {Uuid(entry.BetId)},
                    {Text(entry.OriginalTransactionId?.Value)}, {Numeric(entry.BalanceAfter.Value)},
                    {new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = entry.RecordedAt }},
                    {Numeric(expectedPlayerSum.Value)},
                    {new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, Value = entry.Postings.Select(p => p.Account.Player?.Value).ToArray() }},
                    {new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Numeric, Value = entry.Postings.Select(p => p.Amount.Value).ToArray() }}) AS "Value"
                """).ToListAsync(cancellationToken).ConfigureAwait(false);

            return outcome.Single() switch
            {
                "appended" => LedgerAppendOutcome.Appended,
                "duplicate" => LedgerAppendOutcome.DuplicateTransaction,
                "stale" => LedgerAppendOutcome.StaleBalance,
                var other => throw new InvalidOperationException($"Unexpected outcome '{other}' of wallet.try_append_entry."),
            };
        }
    }

    private static NpgsqlParameter Text(string? value) => new() { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)value ?? DBNull.Value };

    private static NpgsqlParameter Uuid(Guid? value) => new() { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)value ?? DBNull.Value };

    private static NpgsqlParameter Numeric(decimal value) => new() { NpgsqlDbType = NpgsqlDbType.Numeric, Value = value };

    private static Task<decimal> SumOfAsync(WalletDbContext db, LedgerAccount account, CancellationToken cancellationToken)
    {
        var postings = db.Postings.AsNoTracking()
            .Where(p => p.OperatorId == account.Operator.Value && p.Currency == account.Currency.Code);
        postings = account.Player is { } player
            ? postings.Where(p => p.PlayerId == player.Value)
            : postings.Where(p => p.PlayerId == null);
        return postings.SumAsync(p => p.Amount, cancellationToken);
    }

    private static LedgerEntry ToEntry(LedgerEntryRecord record) =>
        new(
            new TransactionId(record.TransactionId),
            Enum.Parse<LedgerEntryKind>(record.Kind),
            new PlayerRef(new OperatorId(record.OperatorId), new PlayerId(record.PlayerId)),
            new Currency(record.Currency),
            record.RoundId is { } round ? new RoundId(round) : null,
            record.BetId,
            record.OriginalTransactionId is { } original ? new TransactionId(original) : null,
            [
                .. record.Postings.OrderBy(p => p.Line).Select(p => new LedgerPosting(
                    new LedgerAccount(new OperatorId(p.OperatorId), new Currency(p.Currency), p.PlayerId is { } player ? new PlayerId(player) : null),
                    new Money(p.Amount))),
            ],
            new Money(record.BalanceAfter),
            record.RecordedAt);
}
