namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>Position in the personal history (SPEC-0025 §6): the bets strictly before it by <c>placedAt</c>, then by <c>betId</c>.</summary>
/// <param name="PlacedAt">When the last bet of the previous page was accepted.</param>
/// <param name="BetId">The id of that bet.</param>
public sealed record PlayerBetsCursor(DateTimeOffset PlacedAt, Guid BetId);
