using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Rounds.Contracts;

/// <summary>The cash-out was received before the crash; <paramref name="Multiplier"/> is the truncated multiplier at reception.</summary>
public sealed record CashOutAccepted(Multiplier Multiplier) : CashOutDecision;
