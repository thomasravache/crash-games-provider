namespace Apogeu.Betting.Domain;

/// <summary>The bet is now refunded: its round was annulled; <paramref name="Refunded"/> (its full amount) is given back with a rollback of its debit.</summary>
public sealed record BetClosedRefunded(decimal Refunded) : BetClosure;
