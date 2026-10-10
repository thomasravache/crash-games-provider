using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Wallet.Domain.Ledger;

/// <summary>
/// Persistence boundary of the ledger (schema <c>wallet</c>, ADR-0010): entries are only inserted, never updated or deleted.
/// </summary>
public interface IWalletLedger
{
    /// <summary>The entry recorded under <paramref name="transactionId"/>, or <see langword="null"/>.</summary>
    Task<LedgerEntry?> FindAsync(TransactionId transactionId, CancellationToken cancellationToken);

    /// <summary>
    /// The rollback entry recorded for the debit <paramref name="originalTransactionId"/>, whatever its own transaction id, or
    /// <see langword="null"/>. A debit is refunded at most once (SPEC-0021 UT-06). The default answers
    /// <see langword="null"/> for a ledger that does not index rollbacks by their original debit; the adapter then also looks
    /// up the deterministic rollback id of the bet (<c>bet:{betId}:rollback</c>).
    /// </summary>
    Task<LedgerEntry?> FindRollbackOfAsync(TransactionId originalTransactionId, CancellationToken cancellationToken) =>
        Task.FromResult<LedgerEntry?>(null);

    /// <summary>Sum of every posting of <paramref name="account"/> (zero when it has none).</summary>
    Task<Money> SumAsync(LedgerAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="entry"/> and its postings atomically, only if the account of the entry's player still sums to
    /// <paramref name="expectedPlayerSum"/> (optimistic check, serialized per account, so two different debits that together
    /// exceed the balance never both apply). Writes nothing and returns <see cref="LedgerAppendOutcome.DuplicateTransaction"/>
    /// when an entry with the same transaction id exists, or <see cref="LedgerAppendOutcome.StaleBalance"/> when the sum changed.
    /// </summary>
    Task<LedgerAppendOutcome> TryAppendAsync(LedgerEntry entry, Money expectedPlayerSum, CancellationToken cancellationToken);
}
