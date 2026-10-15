using System.Globalization;

namespace Apogeu.TestKit.Performance;

/// <summary>
/// Local time budgets of the performance tests, scaled per machine (SPEC-0052 §3, Emenda v2), resolved from the variables it
/// receives, never from the real environment.
/// </summary>
/// <remarks>
/// <para>
/// Each local budget is calibrated on the reference machine (factor 1). On CI (<c>GITHUB_ACTIONS=true</c>) each test keeps its
/// own CI budget, whatever else the environment says, so no variable loosens the CI. Any other machine declares in
/// <see cref="ScaleVariable"/> the single factor by which it is slower than the reference one, in its shell profile, outside
/// the repository (the 4 vCPU development VM uses 5: its worst cash-out burst measured 315 ms, 4.2× the 75 ms budget).
/// </para>
/// <para>
/// These budgets guard against regressions on the machine running the tests; the real latency targets on production
/// infrastructure belong to the capacity spec (SPEC-0048).
/// </para>
/// </remarks>
public static class PerformanceBudget
{
    /// <summary>The variable through which each machine declares the factor applied to every local budget.</summary>
    public const string ScaleVariable = "APOGEU_PERF_BUDGET_SCALE";

    /// <summary>
    /// The budget for the given environment (a lookup from variable name to value, <c>null</c> when absent): <paramref name="ci"/>
    /// on CI, otherwise <paramref name="local"/> × the machine factor; and where it came from ("CI", "reference machine" or
    /// "<see cref="ScaleVariable"/>=value"), for the failure message.
    /// </summary>
    /// <exception cref="InvalidOperationException">The variable is set but is not a number greater than zero.</exception>
    public static (TimeSpan Budget, string Source) Resolve(TimeSpan local, TimeSpan ci, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (string.Equals(environment("GITHUB_ACTIONS"), "true", StringComparison.Ordinal))
        {
            return (ci, "CI");
        }

        var value = environment(ScaleVariable);
        if (string.IsNullOrEmpty(value))
        {
            return (local, "reference machine");
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) || !double.IsFinite(scale) || scale <= 0)
        {
            throw new InvalidOperationException(
                $"{ScaleVariable} must be a number greater than zero in the invariant culture (e.g. 5 or 1.5), but was '{value}'.");
        }

        return (local * scale, $"{ScaleVariable}={value}");
    }

    /// <summary>The same as <see cref="Resolve(TimeSpan, TimeSpan, Func{string, string?})"/> for a test whose CI budget is its local one.</summary>
    /// <exception cref="InvalidOperationException">The variable is set but is not a number greater than zero.</exception>
    public static (TimeSpan Budget, string Source) Resolve(TimeSpan local, Func<string, string?> environment) =>
        Resolve(local, local, environment);
}
