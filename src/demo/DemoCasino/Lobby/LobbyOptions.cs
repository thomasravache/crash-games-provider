namespace Apogeu.DemoCasino.Lobby;

/// <summary>Configuration section <c>DemoCasino:Lobby</c> (SPEC-0044 §6, Emenda v1).</summary>
internal sealed class LobbyOptions
{
    public const string SectionName = "DemoCasino:Lobby";

    /// <summary>The codes shown with the "Novo" badge when <see cref="NewGames"/> is not configured.</summary>
    public static readonly IReadOnlyList<string> DefaultNewGames = ["pipa"];

    /// <summary>
    /// Codes of the games marked <c>isNew</c>. When configured it replaces <see cref="DefaultNewGames"/>: the default is not a
    /// property initializer because the configuration binder appends to an initialized collection instead of replacing it.
    /// </summary>
    public string[]? NewGames { get; set; }

    public IReadOnlySet<string> NewGameCodes() => (NewGames ?? DefaultNewGames).ToHashSet(StringComparer.Ordinal);
}
