namespace Apogeu.Betting.Domain;

/// <summary>Outcome of <see cref="Bet.CashOut"/>: <see cref="BetCashOutAccepted"/> or <see cref="BetCashOutRejected"/>.</summary>
public abstract record BetCashOut
{
    private protected BetCashOut()
    {
    }
}
