namespace Apogeu.Betting.Domain;

/// <summary>The bet cannot be cashed out (e.g. <c>BET_NOT_ACTIVE</c>); it is unchanged.</summary>
public sealed record BetCashOutRejected(string Code) : BetCashOut;
