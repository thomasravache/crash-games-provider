#pragma warning disable MA0025 // Scaffold of SPEC-0008 (no logic yet): implemented in the green phase.

namespace Apogeu.Rounds.Domain;

/// <summary>
/// Growth function of the multiplier (SPEC-0008 §4): <c>m(t) = e^(k·t)</c>, with <c>t</c> in milliseconds since the start of
/// the flight and <c>k</c> per room. The crash instant is <c>t_c = ln(crash)/k</c> (not rounded).
/// </summary>
public sealed record GrowthCurve(double K)
{
    /// <summary>Default growth constant per millisecond (≈ 2x in 11.6 s).</summary>
    public const double DefaultK = 0.00006;

    /// <summary><c>t_c = ln(crash)/k</c>, not rounded: the time from the start of the flight to the crash.</summary>
    public TimeSpan TimeToCrash(decimal crashPoint) => throw new NotImplementedException();

    /// <summary>
    /// The displayed multiplier at <paramref name="elapsed"/> of flight: <c>floor(e^(k·t)·100)/100</c>, and exactly
    /// <paramref name="crashPoint"/> when <c>t ≥ t_c</c>.
    /// </summary>
    public decimal MultiplierAt(TimeSpan elapsed, decimal crashPoint) => throw new NotImplementedException();
}
