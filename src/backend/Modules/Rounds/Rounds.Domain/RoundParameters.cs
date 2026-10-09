namespace Apogeu.Rounds.Domain;

/// <summary>
/// The room parameters a round is played with, fixed when its bets open: the RTP and cap of its crash point (SPEC-0007) and
/// the growth constant of its flight. Persisted with the round, so a later change of the room's configuration (or its
/// removal) changes neither the resumed flight nor the verification of the round.
/// </summary>
/// <param name="Rtp">Return to player, 0.90–0.99.</param>
/// <param name="Cap">Maximum crash point, ≥ 1.00.</param>
/// <param name="GrowthK">Growth constant per millisecond of <c>m(t) = e^(k·t)</c>.</param>
public sealed record RoundParameters(decimal Rtp, decimal Cap, double GrowthK)
{
    /// <summary>The parameters of the room's current configuration.</summary>
    public static RoundParameters Of(SalaConfig sala)
    {
        ArgumentNullException.ThrowIfNull(sala);
        return new RoundParameters(sala.Rtp, sala.Cap, sala.GrowthK);
    }
}
