namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// Names of the ledger tables in PostgreSQL (SPEC-0021 §8): read directly by the integration tests (IT-01, IT-06), which
/// check the insert-only trigger and that every entry's postings sum to zero.
/// </summary>
public static class WalletLedgerTables
{
    /// <summary>Schema of the module.</summary>
    public const string Schema = "wallet";

    /// <summary>Ledger entries, one row per transaction id.</summary>
    public const string Entries = "ledger_entries";

    /// <summary>Postings of the entries, at least two per entry.</summary>
    public const string Postings = "ledger_postings";

    /// <summary>Guards of the resend chains of the seamless wallet (SPEC-0046), one per pending transaction.</summary>
    public const string ResendChains = "resend_chains";

    /// <summary>Transaction id column, in both tables (the postings reference their entry by it).</summary>
    public const string TransactionIdColumn = "transaction_id";

    /// <summary>Signed amount column of the postings.</summary>
    public const string AmountColumn = "amount";
}
