namespace Apogeu.BuildingBlocks.Contracts;

/// <summary>Identifier of an operator (B2B customer), for example <c>demo</c> (SPEC-0009, SPEC-0020); shared by the modules.</summary>
public readonly record struct OperatorId(string Value);
