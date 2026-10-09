namespace Apogeu.Fairness.Contracts;

/// <summary>The crash point of a round: a multiplier with 2 decimals, between 1.00 and the cap.</summary>
public readonly record struct CrashPoint(decimal Value);
