using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// Double of <see cref="IWalletLedger"/> whose database is down: every call throws <paramref name="failure"/> (a transient
/// failure such as an <c>NpgsqlException</c> or a timeout), as the PostgreSQL ledger would (UT-07).
/// </summary>
internal sealed class FailingWalletLedger(Func<Exception> failure) : IWalletLedger
{
    public Task<LedgerEntry?> FindAsync(TransactionId transactionId, CancellationToken cancellationToken) => throw failure();

    public Task<Money> SumAsync(LedgerAccount account, CancellationToken cancellationToken) => throw failure();

    public Task<LedgerAppendOutcome> TryAppendAsync(LedgerEntry entry, Money expectedPlayerSum, CancellationToken cancellationToken) => throw failure();
}
