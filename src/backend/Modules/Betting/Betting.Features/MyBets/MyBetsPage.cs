namespace Apogeu.Betting.Features.MyBets;

/// <summary>
/// <c>200</c> of <c>GET /api/v1/me/bets</c> (SPEC-0025 §6): the bets of the session's player by <c>placedAt</c> descending,
/// ties by <c>betId</c> descending.
/// </summary>
/// <param name="Items">The page.</param>
/// <param name="NextCursor">Opaque cursor of the next page (the <c>before</c> parameter); <see langword="null"/> on the last one.</param>
public sealed record MyBetsPage(IReadOnlyList<MyBetItem> Items, string? NextCursor);
