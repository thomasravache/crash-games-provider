namespace Apogeu.Rounds.Domain;

/// <summary>
/// Configuration of a room (SPEC-0008 §4, <c>Rounds:Salas:{id}</c>): id <c>^[a-z0-9-]{1,32}$</c>, RTP, cap, growth constant,
/// phase durations and the length of its seed chains.
/// </summary>
public sealed record SalaConfig(
    string Id,
    decimal Rtp,
    decimal Cap,
    double GrowthK,
    TimeSpan BettingDuration,
    TimeSpan CooldownDuration,
    int ChainLength);
