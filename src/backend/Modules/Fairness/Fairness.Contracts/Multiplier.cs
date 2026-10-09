namespace Apogeu.Fairness.Contracts;

/// <summary>A multiplier, such as the cap of a room (decimal, ADR-0002). A cap below 1.00 is rejected with <see cref="InvalidCapException"/>.</summary>
public readonly record struct Multiplier(decimal Value);
