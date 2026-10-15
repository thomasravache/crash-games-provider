using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// Client method <c>LiveBetsBatch</c> sent to the room's group every 200 ms when there is news (SPEC-0025 §6): the bets and
/// cash-outs of the window and the totals of the round.
/// </summary>
/// <param name="RoundId">The round of the window.</param>
/// <param name="Placed">Up to 100 bets placed in the window, the highest amounts first.</param>
/// <param name="CashedOut">Up to 100 cash-outs of the window.</param>
/// <param name="TotalBets">Every bet of the round, including those left out of <paramref name="Placed"/>.</param>
/// <param name="TotalPlayers">Distinct players with a bet in the round.</param>
/// <param name="TotalWagered">Sum of the amounts of the round.</param>
public sealed record LiveBetsBatch(
    Guid RoundId,
    IReadOnlyList<LiveBet> Placed,
    IReadOnlyList<LiveCashOut> CashedOut,
    int TotalBets,
    int TotalPlayers,
    Money TotalWagered);
