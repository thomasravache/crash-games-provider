using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Domain;

/// <summary>The settlement summary of a round's closed bets (SPEC-0024 §6), pure: one summary per operator and currency.</summary>
public static class RoundSettlementCalculator
{
    /// <summary>
    /// Sums <paramref name="bets"/> (the round's bets, closed: lost, cashed out or refunded) per operator and currency:
    /// wagered, paid (cash-out payouts), refunded and <c>ggr = wagered − paid − refunded</c>.
    /// </summary>
    public static IReadOnlyList<RoundSettlementSummary> Summarize(IEnumerable<Bet> bets)
    {
        ArgumentNullException.ThrowIfNull(bets);
        return [.. bets
            .Where(bet => bet.Status is BetStatus.Lost or BetStatus.CashedOut or BetStatus.Refunded)
            .GroupBy(bet => (bet.Owner.OperatorId, bet.Owner.Currency))
            .OrderBy(group => group.Key.OperatorId.Value, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Currency.Code, StringComparer.Ordinal)
            .Select(group => Summary(group.Key.OperatorId, group.Key.Currency, [.. group]))];
    }

    private static RoundSettlementSummary Summary(OperatorId operatorId, Currency currency, IReadOnlyCollection<Bet> bets)
    {
        var wagered = bets.Sum(bet => bet.Amount);
        var paid = bets.Where(bet => bet.Status == BetStatus.CashedOut).Sum(bet => bet.Payout ?? 0m);
        // What the refunds returned: 0,00 for one the wallet answered TransactionNotFound (SPEC-0024 review G4).
        var refunded = bets.Where(bet => bet.Status == BetStatus.Refunded).Sum(bet => bet.Refunded ?? bet.Amount);
        return new RoundSettlementSummary(operatorId, currency, bets.Count, wagered, paid, refunded, wagered - paid - refunded);
    }
}
