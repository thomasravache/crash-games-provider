namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Configuration of the room <c>demo</c> of the integration hosts (<c>Rounds:Salas:demo</c>, SPEC-0008 §4): short phases, a
/// short chain and a low cap, so a round lasts seconds.
/// </summary>
internal sealed record SalaSettings(
    string Rtp = "0.97",
    string Cap = "2.00",
    string GrowthK = "0.00006",
    string BettingDuration = "00:00:01",
    string CooldownDuration = "00:00:00.500",
    int ChainLength = 300)
{
    public const string SalaId = "demo";

    public double K => double.Parse(GrowthK, System.Globalization.CultureInfo.InvariantCulture);
}
