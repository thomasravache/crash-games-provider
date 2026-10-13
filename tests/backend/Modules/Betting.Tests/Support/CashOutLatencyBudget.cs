using System.Globalization;

namespace Apogeu.Betting.Tests.Support;

/// <summary>The local p99 budget of the cash-out burst (SPEC-0052 §3), resolved from the variables it receives, never from the real environment.</summary>
/// <remarks>
/// SPEC-0023 §5 targets p99 ≤ 50 ms; since SPEC-0024 records the request before asking the leader (one more write), the reference
/// machine measures 52–54 ms, so its local guard is 75 ms until SPEC-0048 brings it back to 50 ms. The shared GitHub runner
/// (2 vCPU, Postgres container on the same host) cannot hold that burst; there the test guards against regressions with 3 s
/// (1,7 s measured after SPEC-0024), whatever else the environment says, so no variable loosens the CI. Any other machine
/// declares its own budget in <see cref="Variable"/> (the 4 vCPU development VM measures 145–315 ms and uses 400 ms); the
/// 50 ms target on real infrastructure belongs to the capacity spec (SPEC-0048).
/// </remarks>
internal static class CashOutLatencyBudget
{
    /// <summary>The variable through which each machine declares its own local budget, in milliseconds.</summary>
    public const string Variable = "APOGEU_CASHOUT_P99_BUDGET_MS";

    private static readonly TimeSpan Ci = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReferenceMachine = TimeSpan.FromMilliseconds(75);

    /// <summary>The p99 budget for the given environment (a lookup from variable name to value, <c>null</c> when absent).</summary>
    public static TimeSpan Resolve(Func<string, string?> environment) => ResolveWithSource(environment).Budget;

    /// <summary>The p99 budget for the given environment and where it came from ("CI", "reference machine" or <see cref="Variable"/>).</summary>
    /// <exception cref="InvalidOperationException">The variable is set but is not an integer greater than zero.</exception>
    public static (TimeSpan Budget, string Source) ResolveWithSource(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (string.Equals(environment("GITHUB_ACTIONS"), "true", StringComparison.Ordinal))
        {
            return (Ci, "CI");
        }

        var value = environment(Variable);
        if (string.IsNullOrEmpty(value))
        {
            return (ReferenceMachine, "reference machine");
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds <= 0)
        {
            throw new InvalidOperationException(
                $"{Variable} must be an integer number of milliseconds greater than zero, but was '{value}'.");
        }

        return (TimeSpan.FromMilliseconds(milliseconds), Variable);
    }
}
