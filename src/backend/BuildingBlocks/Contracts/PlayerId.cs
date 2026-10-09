namespace Apogeu.BuildingBlocks.Contracts;

/// <summary>Pseudonymous identifier of a player, as the operator sends it in the launch (SPEC-0009, ADR-0014); shared by the modules.</summary>
public readonly record struct PlayerId(string Value);
