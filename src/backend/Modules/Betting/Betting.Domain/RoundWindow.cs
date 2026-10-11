using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Domain;

/// <summary>The current round of a room as Betting sees it (SPEC-0022 §4: the phase boundary is the server clock).</summary>
/// <param name="RoundId">The round.</param>
/// <param name="RoundNumber">Sequential number of the round in the room.</param>
/// <param name="BettingEndsAt">Server instant the betting phase of the round ends.</param>
/// <param name="Phase">Last phase received for the round.</param>
public sealed record RoundWindow(RoundId RoundId, long RoundNumber, DateTimeOffset BettingEndsAt, RoundPhase Phase);
