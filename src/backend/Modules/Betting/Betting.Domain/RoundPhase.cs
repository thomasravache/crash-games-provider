namespace Apogeu.Betting.Domain;

/// <summary>Phase of the current round of a room as Betting sees it, from the Rounds integration events (SPEC-0022 §6).</summary>
public enum RoundPhase
{
    /// <summary><c>RoundBettingOpened</c>: bets are accepted until <c>BettingEndsAt</c> (server clock).</summary>
    Betting,

    /// <summary><c>RoundFlying</c>: new bets are for the next round.</summary>
    Flying,

    /// <summary><c>RoundCrashed</c> or <c>RoundAnnulled</c>: new bets are for the next round.</summary>
    Ended,
}
