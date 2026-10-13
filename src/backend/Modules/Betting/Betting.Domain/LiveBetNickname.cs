namespace Apogeu.Betting.Domain;

/// <summary>
/// The nickname a room shows for a live bet (SPEC-0025 §6, ADR-0005): the first two letters of the nickname followed by
/// <c>***</c> for every player, so no operator learns the identity of another operator's players.
/// </summary>
public static class LiveBetNickname
{
    /// <summary>Suffix that replaces the rest of the nickname.</summary>
    public const string Mask = "***";

    /// <summary>What is shown for a player without a nickname, before the <see cref="Mask"/>.</summary>
    public const string Fallback = "Jogador";

    /// <summary>
    /// The anonymized <paramref name="nickname"/>: <c>"Marcos"</c> → <c>"Ma***"</c>, <c>"A"</c> → <c>"A***"</c>,
    /// absent → <c>"Jogador***"</c>.
    /// </summary>
    public static string Anonymize(string? nickname) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0025 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025
}
