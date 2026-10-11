namespace Apogeu.Betting.Domain;

/// <summary>The bet cannot be cancelled (e.g. <c>CANCEL_NOT_ALLOWED</c>); it is unchanged.</summary>
public sealed record BetCancellationRejected(string Code) : BetCancellation;
