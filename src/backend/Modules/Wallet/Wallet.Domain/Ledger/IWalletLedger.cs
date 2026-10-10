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

    /// <summary>Sum of every posting of <paramref name="account"/> (zero when it has none).</summary>
    Task<Money> SumAsync(LedgerAccount account, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="entry"/> and its postings atomically; returns <see langword="false"/> and writes nothing
    /// when an entry with the same transaction id already exists (also under concurrency).
    /// </summary>
    Task<bool> TryAppendAsync(LedgerEntry entry, CancellationToken cancellationToken);
}
