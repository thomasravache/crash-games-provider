using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// The ledger in the schema <c>wallet</c> of PostgreSQL (ADR-0010), insert-only (a <c>BEFORE UPDATE OR DELETE</c> trigger
/// refuses any change). It is a singleton: each operation runs on a <see cref="WalletDbContext"/> of a DI scope of its own.
/// </summary>
/// <remarks>
/// <see cref="TryAppendAsync"/> serializes the appends of one player account with a transaction-scoped advisory lock, then
/// checks the transaction id and the expected sum before inserting (one round trip for the lock and the checks, one for the
/// inserts, one for the commit); the primary key on the transaction id is the last
/// guard of idempotency (a same id for another account).
/// </remarks>
internal sealed class PostgresWalletLedger(IServiceScopeFactory scopes) : IWalletLedger
{
    private const string UniqueViolation = "23505";

    public async Task<LedgerEntry?> FindAsync(TransactionId transactionId, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
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

        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
            return new Money(await SumOfAsync(db, account, cancellationToken).ConfigureAwait(false));
        }
    }

    public async Task<LedgerAppendOutcome> TryAppendAsync(LedgerEntry entry, Money expectedPlayerSum, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var account = LedgerAccount.OfPlayer(entry.Player, entry.Currency);
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

            // The only SaveChanges runs inside this transaction: no savepoint round trip before it.
            db.Database.AutoSavepointsEnabled = false;
            var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var (duplicate, sum) = await LockAndReadAsync(db, entry.TransactionId, account, cancellationToken).ConfigureAwait(false);
                if (duplicate)
                {
                    return LedgerAppendOutcome.DuplicateTransaction;
                }

                if (sum != expectedPlayerSum.Value)
                {
                    return LedgerAppendOutcome.StaleBalance;
                }

                db.Entries.Add(ToRecord(entry));
                try
                {
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: UniqueViolation })
                {
                    return LedgerAppendOutcome.DuplicateTransaction;
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return LedgerAppendOutcome.Appended;
            }
        }
    }

    /// <summary>
    /// In one round trip: takes the transaction-scoped advisory lock of <paramref name="account"/> (one writer per account
    /// until commit or rollback), then — in a statement of its own, so its snapshot sees what the previous holder committed —
    /// reads whether <paramref name="transactionId"/> exists and the account's sum.
    /// </summary>
    private static async Task<(bool Duplicate, decimal Sum)> LockAndReadAsync(
        WalletDbContext db, TransactionId transactionId, LedgerAccount account, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var batch = new NpgsqlBatch(connection) { Transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction() };
        await using (batch.ConfigureAwait(false))
        {
            var lockCommand = new NpgsqlBatchCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))");
            lockCommand.Parameters.Add(new NpgsqlParameter { Value = AccountKey(account) });
            var readCommand = new NpgsqlBatchCommand(
                "SELECT EXISTS (SELECT 1 FROM wallet.ledger_entries WHERE transaction_id = $1), "
                + "COALESCE((SELECT sum(amount) FROM wallet.ledger_postings WHERE operator_id = $2 AND currency = $3 AND player_id = $4), 0)");
            readCommand.Parameters.Add(new NpgsqlParameter { Value = transactionId.Value });
            readCommand.Parameters.Add(new NpgsqlParameter { Value = account.Operator.Value });
            readCommand.Parameters.Add(new NpgsqlParameter { Value = account.Currency.Code });
            readCommand.Parameters.Add(new NpgsqlParameter { Value = account.Player!.Value.Value });
            batch.BatchCommands.Add(lockCommand);
            batch.BatchCommands.Add(readCommand);

            var reader = await batch.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return (reader.GetBoolean(0), reader.GetDecimal(1));
            }
        }
    }

    private static Task<decimal> SumOfAsync(WalletDbContext db, LedgerAccount account, CancellationToken cancellationToken)
    {
        var postings = db.Postings.AsNoTracking()
            .Where(p => p.OperatorId == account.Operator.Value && p.Currency == account.Currency.Code);
        postings = account.Player is { } player
            ? postings.Where(p => p.PlayerId == player.Value)
            : postings.Where(p => p.PlayerId == null);
        return postings.SumAsync(p => p.Amount, cancellationToken);
    }

    /// <summary>Key of the advisory lock of an account; the separator cannot appear in the ids (control character).</summary>
    private static string AccountKey(LedgerAccount account) =>
        string.Join('\u001f', "wallet", account.Operator.Value, account.Currency.Code, account.Player?.Value ?? string.Empty);

    private static LedgerEntryRecord ToRecord(LedgerEntry entry)
    {
        var record = new LedgerEntryRecord
        {
            TransactionId = entry.TransactionId.Value,
            Kind = entry.Kind.ToString(),
            OperatorId = entry.Player.OperatorId.Value,
            PlayerId = entry.Player.PlayerId.Value,
            Currency = entry.Currency.Code,
            RoundId = entry.RoundId?.Value,
            BetId = entry.BetId,
            OriginalTransactionId = entry.OriginalTransactionId?.Value,
            BalanceAfter = entry.BalanceAfter.Value,
            RecordedAt = entry.RecordedAt,
        };
        short line = 0;
        foreach (var posting in entry.Postings)
        {
            record.Postings.Add(new LedgerPostingRecord
            {
                TransactionId = record.TransactionId,
                Line = ++line,
                OperatorId = posting.Account.Operator.Value,
                Currency = posting.Account.Currency.Code,
                PlayerId = posting.Account.Player?.Value,
                Amount = posting.Amount.Value,
            });
        }

        return record;
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
