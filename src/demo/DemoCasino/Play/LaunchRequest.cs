using Apogeu.DemoCasino.Session;

namespace Apogeu.DemoCasino.Play;

/// <summary>
/// The body of <c>POST /api/v1/operator/launch</c> of the Apogeu (SPEC-0009@1, <c>LaunchRequest</c>), exactly its fields
/// (SPEC-0045 §4, Emenda v1): the player, nickname and currency of the casino session, the game of the route, demo mode and
/// pt-BR. Serialized with the web defaults (camelCase).
/// </summary>
internal sealed record LaunchRequest(string PlayerId, string Nickname, string Currency, string GameCode, string Mode, string Language)
{
    public const string DemoMode = "demo";
    public const string CasinoLanguage = "pt-BR";

    public static LaunchRequest For(CasinoPlayer player, string gameCode)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentException.ThrowIfNullOrEmpty(gameCode);
        return new LaunchRequest(player.PlayerId, player.Nickname, player.Currency, gameCode, DemoMode, CasinoLanguage);
    }
}
