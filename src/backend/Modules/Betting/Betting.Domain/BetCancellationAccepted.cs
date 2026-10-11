namespace Apogeu.Betting.Domain;

/// <summary>The bet is now cancelled; <paramref name="Refunded"/> (its full amount) is given back with a rollback of its debit.</summary>
public sealed record BetCancellationAccepted(decimal Refunded) : BetCancellation;
