using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Contracts;

/// <summary>
/// The immutable settlement summary of a round for one operator and currency (SPEC-0024 §6), unique per
/// (<paramref name="RoundId"/>, <paramref name="OperatorId"/>, <paramref name="Currency"/>).
/// </summary>
/// <param name="RoundId">The round.</param>
/// <param name="OperatorId">Operator of the bets.</param>
/// <param name="Currency">Currency of the bets.</param>
/// <param name="BetsCount">How many bets of the round the summary covers.</param>
/// <param name="TotalWagered">Sum of the amounts of those bets.</param>
/// <param name="TotalPaid">Sum of the cash-out payouts.</param>
/// <param name="TotalRefunded">Sum of the refunds of an annulled round.</param>
/// <param name="Ggr">Gross gaming revenue: <c>totalWagered − totalPaid − totalRefunded</c>.</param>
/// <param name="SettledAt">When the round was settled (server clock).</param>
public sealed record RoundSettlement(
    RoundId RoundId,
    OperatorId OperatorId,
    Currency Currency,
    int BetsCount,
    Money TotalWagered,
    Money TotalPaid,
    Money TotalRefunded,
    Money Ggr,
    DateTimeOffset SettledAt);
