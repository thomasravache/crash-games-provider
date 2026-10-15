using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>Result of <c>JoinSalaBets</c> (SPEC-0025 §6): the bets of the current round of the session's room.</summary>
/// <param name="RoundId">The current round.</param>
/// <param name="TotalBets">Every bet of the round.</param>
/// <param name="TotalPlayers">Distinct players with a bet in the round.</param>
/// <param name="TotalWagered">Sum of the amounts of the round.</param>
/// <param name="Bets">Up to 100 bets, the highest amounts first.</param>
public sealed record LiveBetsSnapshot(
    Guid RoundId,
    int TotalBets,
    int TotalPlayers,
    Money TotalWagered,
    IReadOnlyList<LiveBet> Bets);
