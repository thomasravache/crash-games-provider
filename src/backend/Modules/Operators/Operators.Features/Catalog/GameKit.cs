using Apogeu.Operators.Contracts;

namespace Apogeu.Operators.Features.Catalog;

/// <summary>
/// The visual kit of a game, served by the game client (SPEC-0044 §6, Emenda v1):
/// <c>{GameBaseUrl}/kit/{code}/thumb.png</c> (miniature) and <c>{GameBaseUrl}/kit/{code}/banner.png</c> (banner).
/// </summary>
internal static class GameKit
{
    public static string ThumbnailUrl(Uri gameBaseUrl, GameCode code) => ArtUrl(gameBaseUrl, code, "thumb.png");

    public static string BannerUrl(Uri gameBaseUrl, GameCode code) => ArtUrl(gameBaseUrl, code, "banner.png");

    private static string ArtUrl(Uri gameBaseUrl, GameCode code, string file) =>
        new UriBuilder(gameBaseUrl)
        {
            Path = $"{gameBaseUrl.AbsolutePath.TrimEnd('/')}/kit/{Uri.EscapeDataString(code.Value)}/{file}",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri.AbsoluteUri;
}
