namespace Apogeu.Rounds.Contracts;

/// <summary>Integration event (SPEC-0022 §6, ADR-0002 outbox): the flight of the round started at <paramref name="StartedAt"/>.</summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">The round.</param>
/// <param name="StartedAt">Server instant the flight started.</param>
/// <param name="GrowthK">Growth constant of the flight, <c>m(t) = e^(k·t)</c> with <c>t</c> in milliseconds.</param>
public sealed record RoundFlying(string SalaId, RoundId RoundId, DateTimeOffset StartedAt, decimal GrowthK);
