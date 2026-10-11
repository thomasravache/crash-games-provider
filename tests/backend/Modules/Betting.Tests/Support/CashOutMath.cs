namespace Apogeu.Betting.Tests.Support;

/// <summary>The rules of SPEC-0023 §6 computed by the tests on their own (the oracle of the integration tests).</summary>
internal static class CashOutMath
{
    /// <summary><c>floor(e^(k·t)·100)/100</c>, 1,00 before the flight.</summary>
    public static decimal MultiplierAt(double k, TimeSpan elapsed) =>
        elapsed <= TimeSpan.Zero ? 1.00m : Math.Floor((decimal)Math.Exp(k * elapsed.TotalMilliseconds) * 100m) / 100m;

    /// <summary><c>min(floor(amount × multiplier × 100)/100, maxWin)</c>.</summary>
    public static decimal Payout(decimal amount, decimal multiplier, decimal maxWin = 100_000.00m) =>
        Math.Min(Math.Floor(amount * multiplier * 100m) / 100m, maxWin);

    /// <summary><c>ln(multiplier)/k</c>, not rounded.</summary>
    public static TimeSpan TimeTo(double k, decimal multiplier) => TimeSpan.FromMilliseconds(Math.Log((double)multiplier) / k);
}
