namespace Apogeu.Rounds.Contracts;

/// <summary>States of a round (SPEC-0008 §2): <c>Betting → Flying → Crashed</c>, with <c>Annulled</c> as the alternative terminal.</summary>
public enum RoundState
{
    /// <summary>Bets are open; the commitment of the round is public.</summary>
    Betting,

    /// <summary>The multiplier grows from <c>startedAt</c>; the crash point is known only to the conductor, in memory.</summary>
    Flying,

    /// <summary>Terminal: the round crashed and its seed was revealed.</summary>
    Crashed,

    /// <summary>Terminal: the round could not be resumed within the deadline (ADR-0018).</summary>
    Annulled,
}
