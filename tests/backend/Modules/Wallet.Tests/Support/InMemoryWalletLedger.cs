using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// Test double of the persistence boundary (<see cref="IWalletLedger"/>) for the unit tests of SPEC-0021: the insert-only
/// ledger in memory, unique by transaction id, with the recorded entries visible to the test.
/// </summary>
internal sealed class InMemoryWalletLedger : IWalletLedger
{
    private readonly Lock _gate = new();
    private readonly List<LedgerEntry> _entries = [];

    /// <summary>Every entry appended, in order.</summary>
    public IReadOnlyList<LedgerEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public Task<LedgerEntry?> FindAsync(TransactionId transactionId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.Find(entry => entry.TransactionId == transactionId));
        }
    }

    public Task<Money> SumAsync(LedgerAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        lock (_gate)
        {
            var sum = _entries.SelectMany(entry => entry.Postings).Where(posting => posting.Account == account).Sum(posting => posting.Amount.Value);
            return Task.FromResult(new Money(sum));
        }
    }

    public Task<bool> TryAppendAsync(LedgerEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            if (_entries.Exists(existing => existing.TransactionId == entry.TransactionId))
            {
                return Task.FromResult(false);
            }

            _entries.Add(entry);
            return Task.FromResult(true);
        }
    }
}
