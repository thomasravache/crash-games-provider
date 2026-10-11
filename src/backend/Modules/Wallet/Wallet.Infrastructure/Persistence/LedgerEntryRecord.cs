namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>A row of <c>wallet.ledger_entries</c> (SPEC-0021 §2): one per transaction id, inserted once and never changed.</summary>
internal sealed class LedgerEntryRecord
{
    public required string TransactionId { get; set; }

    /// <summary>Name of the <c>LedgerEntryKind</c>.</summary>
    public required string Kind { get; set; }

    public required string OperatorId { get; set; }

    /// <summary>Pseudonymous player id (ADR-0014).</summary>
    public required string PlayerId { get; set; }

    /// <summary>ISO 4217 code of the currency of every posting.</summary>
    public required string Currency { get; set; }

    public Guid? RoundId { get; set; }

    public Guid? BetId { get; set; }

    public string? OriginalTransactionId { get; set; }

    /// <summary>Balance of the player right after the entry: the result a repeated transaction gets back.</summary>
    public decimal BalanceAfter { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public List<LedgerPostingRecord> Postings { get; } = [];
}
