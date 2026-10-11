namespace Apogeu.Rounds.Contracts;

/// <summary>The current round of a room as <see cref="ISalaPhase"/> reads it.</summary>
/// <param name="RoundId">The round.</param>
/// <param name="RoundNumber">Sequential number of the round in the room.</param>
/// <param name="State">State of the round.</param>
/// <param name="BettingEndsAt">Server instant the betting phase ends.</param>
public sealed record SalaRound(RoundId RoundId, long RoundNumber, RoundState State, DateTimeOffset BettingEndsAt);
