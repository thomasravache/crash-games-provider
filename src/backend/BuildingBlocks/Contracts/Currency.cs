namespace Apogeu.BuildingBlocks.Contracts;

/// <summary>Currency of a monetary amount: an ISO 4217 code such as <c>BRL</c> (SPEC-0020 §4); shared by the modules.</summary>
public readonly record struct Currency(string Code);
