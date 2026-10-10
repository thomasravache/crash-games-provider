namespace Apogeu.Rounds.Contracts;

/// <summary>Integration event (SPEC-0022 §6, ADR-0002 outbox): the round was annulled (ADR-0018).</summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">The round.</param>
/// <param name="Reason">Reason recorded on the round (e.g. <c>RESUME_DEADLINE_EXCEEDED</c>).</param>
public sealed record RoundAnnulled(string SalaId, RoundId RoundId, string Reason);
