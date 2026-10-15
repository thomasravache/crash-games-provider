namespace Apogeu.Operators.Contracts;

/// <summary>How the operator keeps the balance of its players (SPEC-0046 §6, ADR-0007): <c>walletMode demo | seamless</c>.</summary>
public enum WalletMode
{
    /// <summary>Fictitious balance kept by the Apogeu (demo adapter, SPEC-0021). The default of every operator.</summary>
    Demo,

    /// <summary>The operator's own wallet, called over HTTP (public operator wallet API).</summary>
    Seamless,
}
