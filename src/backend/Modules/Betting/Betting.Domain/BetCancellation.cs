namespace Apogeu.Betting.Domain;

/// <summary>Outcome of <see cref="Bet.Cancel"/>: <see cref="BetCancellationAccepted"/> or <see cref="BetCancellationRejected"/>.</summary>
public abstract record BetCancellation
{
    private protected BetCancellation()
    {
    }
}
