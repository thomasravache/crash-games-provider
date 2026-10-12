namespace Apogeu.Betting.Domain;

/// <summary>The bet is not closed by its round's end (e.g. already cashed out, lost or refunded): it stays <paramref name="Status"/>.</summary>
public sealed record BetClosureSkipped(BetStatus Status) : BetClosure;
