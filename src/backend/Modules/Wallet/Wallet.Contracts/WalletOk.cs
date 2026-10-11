namespace Apogeu.Wallet.Contracts;

/// <summary>The operation was applied; <paramref name="BalanceAfter"/> is the balance right after it.</summary>
public sealed record WalletOk(Balance BalanceAfter) : WalletResult;
