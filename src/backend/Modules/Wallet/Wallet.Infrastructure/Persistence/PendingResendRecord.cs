namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// A row of <c>wallet.pending_resends</c> (SPEC-0046 §6): a credit or a rollback sent to the operator wallet (seamless) and
/// not confirmed yet. It is resent with the same transaction id, with exponential backoff, until a definitive answer, and
/// then deleted; it survives a restart of the process (ADR-0002, ADR-0018).
/// </summary>
internal sealed class PendingResendRecord
{
    public required string TransactionId { get; set; }

    /// <summary><c>Credit</c> or <c>Rollback</c>.</summary>
    public required string Kind { get; set; }

    public required string OperatorId { get; set; }

    /// <summary>Pseudonymous player id (ADR-0014).</summary>
    public required string PlayerId { get; set; }

    /// <summary>ISO 4217 code of the currency.</summary>
    public required string Currency { get; set; }

    /// <summary>Amount of a credit; none on a rollback (it gives back exactly the debit).</summary>
    public decimal? Amount { get; set; }

    public Guid? RoundId { get; set; }

    public Guid BetId { get; set; }

    /// <summary>For a rollback, the debit it reverses.</summary>
    public string? OriginalTransactionId { get; set; }

    /// <summary>Attempts made so far.</summary>
    public int Attempts { get; set; }

    /// <summary>When the next attempt is due (also the lease of an attempt in flight).</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
