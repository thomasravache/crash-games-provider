namespace Apogeu.Wallet.Contracts;

/// <summary>
/// Values of <c>status</c> in the responses of the public operator wallet API (docs/integration/operator-wallet-api.md,
/// SPEC-0021 §6, contract v2).
/// </summary>
public static class OperatorWalletStatus
{
    public const string Ok = "OK";

    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";

    public const string Duplicate = "DUPLICATE";

    public const string PlayerNotFound = "PLAYER_NOT_FOUND";

    public const string TransactionNotFound = "TRANSACTION_NOT_FOUND";
}
