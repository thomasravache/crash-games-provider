namespace Apogeu.Rounds.Contracts;

/// <summary>Integration event (SPEC-0022 §6, ADR-0002 outbox): the round crashed at <paramref name="CrashPoint"/>.</summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">The round.</param>
/// <param name="CrashPoint">Multiplier at which the round crashed.</param>
/// <param name="CrashedAt">Server instant of the crash.</param>
public sealed record RoundCrashed(string SalaId, RoundId RoundId, decimal CrashPoint, DateTimeOffset CrashedAt);
