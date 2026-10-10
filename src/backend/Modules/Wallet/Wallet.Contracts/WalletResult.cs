namespace Apogeu.Wallet.Contracts;

/// <summary>
/// Result of a wallet operation (SPEC-0021 §6, contract v2):
/// <c>Ok(BalanceAfter) | InsufficientFunds(Balance) | Duplicate(OriginalResult) | TransactionNotFound(OriginalTransactionId) | Unavailable(retryable)</c>.
/// </summary>
public abstract record WalletResult
{
    private protected WalletResult()
    {
    }
}
