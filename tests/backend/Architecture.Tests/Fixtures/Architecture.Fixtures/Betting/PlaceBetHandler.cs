using Apogeu.Wallet.Domain;

namespace Apogeu.Betting.Features.PlaceBet;

/// <summary>Known violation (UT-01): Betting.Features references Wallet.Domain (ADR-0001).</summary>
public static class PlaceBetHandler
{
    public static LedgerEntry Debit(decimal amount) => new() { Amount = amount };
}
