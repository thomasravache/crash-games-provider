using Apogeu.Betting.Domain;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>A bet of the personal history (SPEC-0025 §6): the bet as it is now and when it was accepted.</summary>
/// <param name="Bet">The bet, with its round (none while queued), its cash-out and its status.</param>
/// <param name="PlacedAt">When the bet was accepted (the instant of its reservation, server clock).</param>
public sealed record PlayerBet(Bet Bet, DateTimeOffset PlacedAt);
