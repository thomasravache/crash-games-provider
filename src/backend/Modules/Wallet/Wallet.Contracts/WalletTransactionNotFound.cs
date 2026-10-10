namespace Apogeu.Wallet.Contracts;

/// <summary>
/// A rollback whose original debit does not exist: nothing is posted. Final result for the caller (there is nothing to
/// give back; do not resend).
/// </summary>
public sealed record WalletTransactionNotFound(TransactionId OriginalTransactionId) : WalletResult;
