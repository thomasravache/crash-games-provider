namespace Apogeu.Betting.Domain;

/// <summary>
/// Outcome of closing a bet with its round (<see cref="Bet.Settle"/>, <see cref="Bet.Refund"/>, SPEC-0024):
/// <see cref="BetClosedLost"/>, <see cref="BetClosedRefunded"/> or <see cref="BetClosureSkipped"/>.
/// </summary>
public abstract record BetClosure
{
    private protected BetClosure()
    {
    }
}
