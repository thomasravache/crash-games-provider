using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>Bets of a new round are open.</summary>
public sealed record RoundBettingMessage(string SalaId, Guid RoundId, long RoundNumber, string Commitment, DateTimeOffset BettingEndsAt);
