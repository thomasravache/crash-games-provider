namespace Apogeu.Rounds.Contracts;

/// <summary>The cash-out was received at or after the crash instant (<c>CASHOUT_TOO_LATE</c>).</summary>
public sealed record CashOutTooLate : CashOutDecision;
