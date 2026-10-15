namespace Apogeu.DemoCasino.Wallet;

/// <summary>The statuses of the public wallet contract (SPEC-0021@4, docs/integration/operator-wallet-api.md §5).</summary>
internal static class WalletStatus
{
    public const string Ok = "OK";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string Duplicate = "DUPLICATE";
    public const string TransactionNotFound = "TRANSACTION_NOT_FOUND";
}
