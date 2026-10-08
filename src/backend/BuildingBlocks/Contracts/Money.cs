namespace Apogeu.BuildingBlocks.Contracts;

/// <summary>
/// Monetary amount of a contract (ADR-0008): travels as a decimal string in JSON (<c>"10.50"</c>) and becomes the
/// branded <c>Money</c> type in TypeScript.
/// </summary>
public readonly record struct Money(decimal Value);
