namespace Apogeu.Rounds.Contracts;

/// <summary>Identifier of a round (SPEC-0008); on the wire it is the bare <see cref="Guid"/>.</summary>
public readonly record struct RoundId(Guid Value);
