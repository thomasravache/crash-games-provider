namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Configuration of the room <c>demo</c> of the integration hosts (<c>Rounds:Salas:demo</c>, SPEC-0008 §4): 3 s of bets, a
/// pause of 1 s and short flights (cap 2,00x at k = 0,0002/ms → at most ≈ 3,5 s), so a test sees several rounds quickly.
/// </summary>
internal sealed record BettingSala(
    string Rtp = "0.97",
    string Cap = "2.00",
    string GrowthK = "0.0002",
    string BettingDuration = "00:00:03",
    string CooldownDuration = "00:00:01",
    int ChainLength = 300)
{
    public const string SalaId = "demo";
}
