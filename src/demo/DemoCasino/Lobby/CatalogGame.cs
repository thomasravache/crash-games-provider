namespace Apogeu.DemoCasino.Lobby;

/// <summary>An item of <c>GET /api/v1/games</c> of the Apogeu (SPEC-0020@3), only the fields the lobby uses.</summary>
internal sealed record CatalogGame(string Code, string Name, string? ThumbnailUrl, string? BannerUrl);
