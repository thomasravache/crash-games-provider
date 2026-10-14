namespace Apogeu.TestKit.Performance;

/// <summary>
/// Local time budgets of the performance tests, scaled per machine (SPEC-0052 §3, Emenda v2), resolved from the variables it
/// receives, never from the real environment.
/// </summary>
/// <remarks>
/// On CI (<c>GITHUB_ACTIONS=true</c>) each test keeps its own CI budget, whatever else the environment says, so no variable
/// loosens the CI. Elsewhere the local budget is multiplied by <see cref="ScaleVariable"/> (1, the reference machine, when
/// absent).
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
    public static (TimeSpan Budget, string Source) Resolve(TimeSpan local, TimeSpan ci, Func<string, string?> environment) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0052 Emenda v2 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025

    /// <summary>The same as <see cref="Resolve(TimeSpan, TimeSpan, Func{string, string?})"/> for a test whose CI budget is its local one.</summary>
    /// <exception cref="InvalidOperationException">The variable is set but is not a number greater than zero.</exception>
    public static (TimeSpan Budget, string Source) Resolve(TimeSpan local, Func<string, string?> environment) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0052 Emenda v2 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025
}
