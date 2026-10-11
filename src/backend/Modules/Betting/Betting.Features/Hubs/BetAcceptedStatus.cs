namespace Apogeu.Betting.Features.Hubs;

/// <summary>Status of an accepted bet (<c>"placed"</c> or <c>"queued"</c> on the wire).</summary>
public enum BetAcceptedStatus
{
    /// <summary>In the current round, whose bets are open.</summary>
    Placed,

    /// <summary>For the next round (made while a round was in flight).</summary>
    Queued,
}
