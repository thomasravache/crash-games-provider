namespace Apogeu.Wallet.Contracts;

/// <summary>The wallet could not be reached; <paramref name="Retryable"/> tells whether the same transaction id may be resent.</summary>
public sealed record WalletUnavailable(bool Retryable) : WalletResult;
