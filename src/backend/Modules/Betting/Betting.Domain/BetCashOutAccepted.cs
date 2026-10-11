namespace Apogeu.Betting.Domain;

/// <summary>
/// The bet is cashed out at <paramref name="Multiplier"/>; <paramref name="Payout"/> is
/// <c>min(floor(amount × multiplier × 100)/100, maxWin)</c>. Repeating the cash-out answers the original values.
/// </summary>
public sealed record BetCashOutAccepted(decimal Multiplier, decimal Payout) : BetCashOut;
