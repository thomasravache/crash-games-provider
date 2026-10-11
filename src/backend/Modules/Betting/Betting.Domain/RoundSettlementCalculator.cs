namespace Apogeu.Betting.Domain;

/// <summary>The settlement summary of a round's closed bets (SPEC-0024 §6), pure: one summary per operator and currency.</summary>
public static class RoundSettlementCalculator
{
    /// <summary>
    /// Sums <paramref name="bets"/> (the round's bets, closed: lost, cashed out or refunded) per operator and currency:
    /// wagered, paid (cash-out payouts), refunded and <c>ggr = wagered − paid − refunded</c>.
    /// </summary>
    public static IReadOnlyList<RoundSettlementSummary> Summarize(IEnumerable<Bet> bets) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0024 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025
}
