namespace Apogeu.Rounds.Features;

/// <summary>Configuration of one room; the defaults are the ones of SPEC-0008 §4.</summary>
public sealed class SalaOptions
{
    public decimal Rtp { get; set; } = 0.97m;

    public decimal Cap { get; set; } = 10000.00m;

    public double GrowthK { get; set; } = 0.00006;

    public TimeSpan BettingDuration { get; set; } = TimeSpan.FromSeconds(6);

    public TimeSpan CooldownDuration { get; set; } = TimeSpan.FromSeconds(3);

    public int ChainLength { get; set; } = 1_000_000;
}
