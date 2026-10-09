namespace Apogeu.BuildingBlocks.Contracts;

/// <summary>
/// Crash multiplier of a contract (ADR-0008): travels as a decimal string in JSON (<c>"2.47"</c>) and becomes the
/// branded <c>Multiplier</c> type in TypeScript.
/// </summary>
public readonly record struct Multiplier(decimal Value);
