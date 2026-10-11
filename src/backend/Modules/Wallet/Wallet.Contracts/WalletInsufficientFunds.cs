namespace Apogeu.Wallet.Contracts;

/// <summary>The debit was refused because the balance does not cover it; nothing was posted.</summary>
public sealed record WalletInsufficientFunds(Balance Balance) : WalletResult;
