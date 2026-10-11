namespace Apogeu.Wallet.Contracts;

/// <summary>The transaction id was already processed; <paramref name="OriginalResult"/> is the result it had then. Nothing new is posted.</summary>
public sealed record WalletDuplicate(WalletResult OriginalResult) : WalletResult;
