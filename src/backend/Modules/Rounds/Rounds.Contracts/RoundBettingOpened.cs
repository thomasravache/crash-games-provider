namespace Apogeu.Rounds.Contracts;

/// <summary>
/// Integration event (SPEC-0022 §6, ADR-0002 outbox): the conductor of <paramref name="SalaId"/> opened the bets of a new
/// round, accepted on the server clock until <paramref name="BettingEndsAt"/>.
/// </summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">The round.</param>
/// <param name="RoundNumber">Sequential number of the round in the room.</param>
/// <param name="BettingEndsAt">Server instant the betting phase ends.</param>
public sealed record RoundBettingOpened(string SalaId, RoundId RoundId, long RoundNumber, DateTimeOffset BettingEndsAt);
