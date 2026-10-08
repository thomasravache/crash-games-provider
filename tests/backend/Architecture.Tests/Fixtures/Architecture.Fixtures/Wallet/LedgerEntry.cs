namespace Apogeu.Wallet.Domain;

/// <summary>Domain type of the Wallet module. Betting.Features must not reference it (UT-01).</summary>
public sealed class LedgerEntry
{
    public decimal Amount { get; init; }
}
