using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Domain;

/// <summary>The settlement totals of a round's bets for one operator and currency (SPEC-0024 §6).</summary>
/// <param name="OperatorId">Operator of the bets.</param>
/// <param name="Currency">Currency of the bets.</param>
/// <param name="BetsCount">How many bets.</param>
/// <param name="TotalWagered">Sum of their amounts.</param>
/// <param name="TotalPaid">Sum of the payouts of the cashed-out ones.</param>
/// <param name="TotalRefunded">Sum of the amounts of the refunded ones.</param>
/// <param name="Ggr"><c>totalWagered − totalPaid − totalRefunded</c>.</param>
public sealed record RoundSettlementSummary(
    OperatorId OperatorId,
    Currency Currency,
    int BetsCount,
    decimal TotalWagered,
    decimal TotalPaid,
    decimal TotalRefunded,
    decimal Ggr);
