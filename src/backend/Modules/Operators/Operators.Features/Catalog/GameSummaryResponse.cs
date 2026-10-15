namespace Apogeu.Operators.Features.Catalog;

/// <summary>
/// An item of <c>200</c> of <c>GET /api/v1/games</c>: a game enabled for the calling operator (SPEC-0020 §6) and the art of
/// its visual kit (SPEC-0020 Emenda v2, SPEC-0044 §6): absolute URLs of the miniature and the banner on the game client.
/// </summary>
public sealed record GameSummaryResponse(string Code, string Name, IReadOnlyList<string> Currencies, string ThumbnailUrl, string BannerUrl);
