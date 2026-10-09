namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>A contract scalar on the wire: a JSON string with <paramref name="Format"/> and an optional <paramref name="Pattern"/>.</summary>
public sealed record ContractScalar(string Format, string? Pattern, bool IsNullable);
