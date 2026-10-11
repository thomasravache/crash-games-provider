namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// A row of <c>wallet.ledger_postings</c>: one side of an entry, a signed amount on the account of a player
/// (<see cref="PlayerId"/> set) or of the operator's house (<see cref="PlayerId"/> null) in one currency.
/// </summary>
internal sealed class LedgerPostingRecord
{
    public required string TransactionId { get; set; }

    /// <summary>Position of the posting in its entry (1, 2, ...).</summary>
    public short Line { get; set; }

    public required string OperatorId { get; set; }

    public required string Currency { get; set; }

    public string? PlayerId { get; set; }

    public decimal Amount { get; set; }
}
