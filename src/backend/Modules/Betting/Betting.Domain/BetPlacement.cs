namespace Apogeu.Betting.Domain;

/// <summary>Outcome of <see cref="Bet.Place"/>: <see cref="BetPlacementAccepted"/> or <see cref="BetPlacementRejected"/>.</summary>
public abstract record BetPlacement
{
    private protected BetPlacement()
    {
    }
}
