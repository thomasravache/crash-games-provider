namespace Apogeu.Wallet.Infrastructure.Messaging;

/// <summary>
/// Durable command (Wolverine, ADR-0002/0018) to send again a credit or a rollback of the seamless wallet (SPEC-0046) whose
/// last attempt got no definitive answer from the operator: scheduled through the outbox with exponential backoff (1 s … 5 min)
/// and resent with the same transaction id until the operator settles it — on any instance, also after a restart.
/// </summary>
/// <param name="Kind">Credit or rollback.</param>
/// <param name="TransactionId">Deterministic id of the operation (the operator deduplicates by it).</param>
/// <param name="OperatorId">Operator of the player.</param>
/// <param name="PlayerId">Pseudonymous player id.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="Amount">Amount of a credit; <see langword="null"/> for a rollback.</param>
/// <param name="RoundId">Round of the bet, when there is one.</param>
/// <param name="BetId">The bet.</param>
/// <param name="OriginalTransactionId">The debit a rollback reverses; <see langword="null"/> for a credit.</param>
/// <param name="Attempt">Attempts already made without a definitive answer (backoff of the next one).</param>
public sealed record ResendOperatorWallet(
    OperatorWalletResendKind Kind,
    string TransactionId,
    string OperatorId,
    string PlayerId,
    string Currency,
    decimal? Amount,
    Guid? RoundId,
    Guid BetId,
    string? OriginalTransactionId,
    int Attempt);
