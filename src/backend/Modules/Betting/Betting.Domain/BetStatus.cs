namespace Apogeu.Betting.Domain;

/// <summary>
/// Life cycle of a bet (SPEC-0022 §6): <c>Placed → Active</c> (the flight started); <c>Queued → Placed</c> (the next round
/// opened its bets); <c>Placed | Queued → Cancelled</c>; <c>Active → CashedOut</c> (SPEC-0023); <c>Active → Lost</c> and <c>Placed | Active → Refunded</c> (SPEC-0024).
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

    /// <summary>Terminal: cashed out during the flight, with its multiplier and payout (SPEC-0023).</summary>
    CashedOut,

    /// <summary>Terminal: active when its round crashed, without a cash-out (SPEC-0024); no wallet movement.</summary>
    Lost,

    /// <summary>Terminal: placed or active in a round that was annulled, its full amount refunded (SPEC-0024).</summary>
    Refunded,
}
