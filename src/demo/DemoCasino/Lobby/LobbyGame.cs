namespace Apogeu.DemoCasino.Lobby;

/// <summary>An item of <c>200</c> of <c>GET /casino/games</c> (SPEC-0044 §6).</summary>
internal sealed record LobbyGame(string Code, string Name, string? ThumbnailUrl, string? BannerUrl, string Category, bool IsNew)
{
    /// <summary>The only category of the catalog: every game of the Apogeu is a crash game (§6, Emenda v1).</summary>
    public const string CrashCategory = "crash";

    /// <summary>
    /// The lobby view of a game of the Apogeu catalog: code, name and kit art passed through, category <c>crash</c>, and
    /// <c>isNew</c> when its code is in <paramref name="newGames"/>. The currencies stay on the server.
    /// </summary>
    public static LobbyGame Of(CatalogGame game, IReadOnlySet<string> newGames)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(newGames);
        return new LobbyGame(game.Code, game.Name, game.ThumbnailUrl, game.BannerUrl, CrashCategory, newGames.Contains(game.Code));
    }
}
