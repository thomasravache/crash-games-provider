namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>What a new bet collided with in <see cref="IBetStore.ReserveAsync"/>.</summary>
public enum BetConflict
{
    /// <summary>Another bet of the player has the same <c>clientBetId</c>.</summary>
    ClientBetId,

    /// <summary>The panel already has a bet for the same round (or for the next one).</summary>
    Panel,
}
