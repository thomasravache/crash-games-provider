namespace Apogeu.Wallet.Contracts;

/// <summary>Error codes of the wallet REST API (SPEC-0021 §6), carried in the <c>code</c> property of problem+json bodies.</summary>
public static class WalletErrorCodes
{
    /// <summary>The demo balance still covers the minimum bet (409).</summary>
    public const string RechargeNotAllowed = "RECHARGE_NOT_ALLOWED";

    /// <summary>The session is not a demo session (403).</summary>
    public const string NotDemo = "NOT_DEMO";
}
