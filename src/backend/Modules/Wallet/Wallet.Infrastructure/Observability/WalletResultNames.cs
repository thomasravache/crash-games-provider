using Apogeu.Wallet.Contracts;

namespace Apogeu.Wallet.Infrastructure.Observability;

/// <summary>Values of the <c>type</c> and <c>result</c> tags of <c>wallet.operations</c>.</summary>
public static class WalletResultNames
{
    public const string Debit = "debit";

    public const string Credit = "credit";

    public const string Rollback = "rollback";

    public const string Ok = "ok";

    public const string InsufficientFunds = "insufficient_funds";

    public const string Duplicate = "duplicate";

    public const string TransactionNotFound = "transaction_not_found";

    public const string Unavailable = "unavailable";

    /// <summary>The <c>result</c> tag of <paramref name="result"/>.</summary>
    public static string Of(WalletResult result) => result switch
    {
        WalletOk => Ok,
        WalletInsufficientFunds => InsufficientFunds,
        WalletDuplicate => Duplicate,
        WalletTransactionNotFound => TransactionNotFound,
        _ => Unavailable,
    };
}
