namespace Apogeu.Operators.Contracts;

/// <summary>Code of a game of the catalog (for example <c>pipa</c>), as the operators send it in the launch (SPEC-0020).</summary>
public readonly record struct GameCode(string Value);
