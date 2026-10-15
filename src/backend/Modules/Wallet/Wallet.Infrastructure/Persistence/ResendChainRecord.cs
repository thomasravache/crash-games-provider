namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>
/// A row of <c>wallet.resend_chains</c> (SPEC-0046 IT-08): the guard of the one resend chain of a credit or a rollback of the
/// seamless wallet. Inserted in the same transaction as the scheduled <c>ResendOperatorWallet</c> (Wolverine outbox) and
/// deleted when the operator settles the operation; while it exists, asking for the same transaction again schedules nothing.
/// </summary>
internal sealed class ResendChainRecord
{
    public required string TransactionId { get; set; }

    /// <summary><c>Credit</c> or <c>Rollback</c>.</summary>
    public required string Kind { get; set; }

    public required string OperatorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
