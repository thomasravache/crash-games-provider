namespace Apogeu.Betting.Domain;

/// <summary>The bet is now lost: its round crashed while it was active without a cash-out (the debit stands).</summary>
public sealed record BetClosedLost : BetClosure;
