namespace Apogeu.Rounds.Domain;

/// <summary>
/// Growth function of the multiplier (SPEC-0008 §4): <c>m(t) = e^(k·t)</c>, with <c>t</c> in milliseconds since the start of
/// the flight and <c>k</c> per room. The crash instant is <c>t_c = ln(crash)/k</c> (not rounded).
/// </summary>
public sealed record GrowthCurve(double K)
{
    /// <summary>Default growth constant per millisecond (≈ 2x in 11.6 s).</summary>
    public const double DefaultK = 0.00006;

    /// <summary>Growth constant per millisecond: positive and finite.</summary>
    public double K { get; } = double.IsFinite(K) && K > 0
        ? K
        : throw new ArgumentOutOfRangeException(nameof(K), K, "The growth constant must be a positive finite number.");

    /// <summary><c>t_c = ln(crash)/k</c>, not rounded: the time from the start of the flight to the crash.</summary>
    public TimeSpan TimeToCrash(decimal crashPoint)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(crashPoint, 1.00m);

        var milliseconds = Math.Log((double)crashPoint) / K;
        return TimeSpan.FromTicks((long)Math.Round(milliseconds * TimeSpan.TicksPerMillisecond, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// The displayed multiplier at <paramref name="elapsed"/> of flight: <c>floor(e^(k·t)·100)/100</c>, and exactly
    /// <paramref name="crashPoint"/> when <c>t ≥ t_c</c>.
    /// </summary>
    public decimal MultiplierAt(TimeSpan elapsed, decimal crashPoint)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(crashPoint, 1.00m);

        if (elapsed >= TimeToCrash(crashPoint))
        {
            return crashPoint;
        }

        if (elapsed <= TimeSpan.Zero)
        {
            return 1.00m;
        }

        var displayed = Math.Floor((decimal)Math.Exp(K * elapsed.TotalMilliseconds) * 100m) / 100m;
        return Math.Min(displayed, crashPoint);
    }
}
