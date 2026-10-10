namespace Apogeu.Betting.Domain;

/// <summary>
/// Life cycle of a bet (SPEC-0022 §6): <c>Placed → Active</c> (the flight started); <c>Queued → Placed</c> (the next round
/// opened its bets); <c>Placed | Queued → Cancelled</c>.
/// </summary>
public enum BetStatus
{
    /// <summary>In a round whose bets are open.</summary>
    Placed,

    /// <summary>In a round in flight (or past it): it can no longer be cancelled.</summary>
    Active,

    /// <summary>Made while a round was in flight: it is for the next round.</summary>
    Queued,

    /// <summary>Terminal: cancelled with the full amount refunded.</summary>
    Cancelled,
}
