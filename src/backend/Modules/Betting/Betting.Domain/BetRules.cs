namespace Apogeu.Betting.Domain;

/// <summary>The rules a bet is validated against (SPEC-0022 §4).</summary>
/// <param name="MinBet">Minimum bet of the operator in the currency (<c>IOperatorLimits</c>, SPEC-0020).</param>
/// <param name="MaxBet">Maximum bet of the operator in the currency (<c>IOperatorLimits</c>, SPEC-0020).</param>
/// <param name="MaxAutoCashOut">The room's cap: the largest automatic cash-out accepted (the smallest is 1.01x).</param>
public sealed record BetRules(decimal MinBet, decimal MaxBet, decimal MaxAutoCashOut);
