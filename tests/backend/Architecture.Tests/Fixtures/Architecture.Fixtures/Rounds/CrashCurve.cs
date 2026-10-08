namespace Apogeu.Rounds.Domain;

/// <summary>Allowed (UT-04 control): the growth exponent of the multiplier curve is not money and may be double.</summary>
public sealed class CrashCurve
{
    public double GrowthK { get; init; }
}
