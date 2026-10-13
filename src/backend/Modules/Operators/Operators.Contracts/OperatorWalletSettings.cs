namespace Apogeu.Operators.Contracts;

/// <summary>
/// Wallet configuration of one operator (SPEC-0046 §6): its <paramref name="Mode"/> and, in seamless mode, the base URL of
/// its wallet (<c>walletUrl</c>, SPEC-0038) and the key the calls are signed with.
/// </summary>
public sealed record OperatorWalletSettings(WalletMode Mode, Uri? WalletUrl, OperatorWalletKey? Key);
