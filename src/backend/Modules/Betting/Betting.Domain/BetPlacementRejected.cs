namespace Apogeu.Betting.Domain;

/// <summary>The bet is refused with a contract error code (<c>BettingErrorCodes</c>); nothing is debited nor persisted.</summary>
public sealed record BetPlacementRejected(string Code) : BetPlacement;
