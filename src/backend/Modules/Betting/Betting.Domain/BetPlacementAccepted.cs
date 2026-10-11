namespace Apogeu.Betting.Domain;

/// <summary>The bet is valid; it is accepted once the wallet confirms its debit (ADR-0007).</summary>
public sealed record BetPlacementAccepted(Bet Bet) : BetPlacement;
