namespace Apogeu.Wallet.Contracts;

/// <summary>Public contract of the Wallet module: other modules may reference it (UT-01 control).</summary>
public sealed record WalletBalanceQuery(Guid PlayerId);
