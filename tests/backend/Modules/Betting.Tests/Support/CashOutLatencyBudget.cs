namespace Apogeu.Betting.Tests.Support;

/// <summary>The local p99 budget of the cash-out burst (SPEC-0052 §3), resolved from the variables it receives, never from the real environment.</summary>
internal static class CashOutLatencyBudget
{
    /// <summary>The variable through which each machine declares its own local budget, in milliseconds.</summary>
    public const string Variable = "APOGEU_CASHOUT_P99_BUDGET_MS";

    /// <summary>The p99 budget for the given environment (a lookup from variable name to value, <c>null</c> when absent).</summary>
    public static TimeSpan Resolve(Func<string, string?> environment) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0052 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025
}
