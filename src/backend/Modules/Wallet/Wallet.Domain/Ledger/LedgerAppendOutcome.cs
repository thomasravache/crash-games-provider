namespace Apogeu.Wallet.Domain.Ledger;

/// <summary>Outcome of <see cref="IWalletLedger.TryAppendAsync"/>.</summary>
public enum LedgerAppendOutcome
{
    /// <summary>The entry and its postings were inserted.</summary>
    Appended,

    /// <summary>An entry with the same transaction id already exists; nothing was written.</summary>
    DuplicateTransaction,

    /// <summary>
    /// The player's account no longer sums to the expected amount (another entry was appended meanwhile); nothing was
    /// written and the caller decides again on the fresh sum.
    /// </summary>
    StaleBalance,
}
