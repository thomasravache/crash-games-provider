namespace Apogeu.Wallet.Domain.Ledger;

/// <summary>What a ledger entry records (SPEC-0021 §2).</summary>
public enum LedgerEntryKind
{
    /// <summary>A bet taken from the player.</summary>
    Debit,

    /// <summary>A win paid to the player.</summary>
    Credit,

    /// <summary>Reversal of an earlier debit.</summary>
    Rollback,

    /// <summary>Demo recharge back to the opening balance.</summary>
    Recharge,
}
