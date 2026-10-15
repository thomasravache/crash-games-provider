using Apogeu.TestKit.Performance;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>
/// The local p99 budget of the cash-out burst (75 ms, 3 s on CI) scaled by the machine factor (SPEC-0052 §3, Emenda v2),
/// resolved from a given environment, never from the real one.
/// </summary>
public sealed class CashOutLatencyBudgetTests
{
    private static readonly TimeSpan Local = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan Ci = TimeSpan.FromSeconds(3);

    [Theory(DisplayName = "Dado GITHUB_ACTIONS=true, com ou sem APOGEU_PERF_BUDGET_SCALE=5, quando o limite de 75 ms é resolvido, então é o de CI (3 s), com origem CI")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    [InlineData(null)]
    [InlineData("5")]
    public void GivenGitHubActionsWhenBudgetIsResolvedThenItIsTheCiBudget(string? scale)
    {
        var environment = Environment(("GITHUB_ACTIONS", "true"), (PerformanceBudget.ScaleVariable, scale));

        var (budget, source) = PerformanceBudget.Resolve(Local, Ci, environment);

        Assert.Equal(TimeSpan.FromSeconds(3), budget);
        Assert.Equal("CI", source);
    }

    [Fact(DisplayName = "Dado um ambiente sem GITHUB_ACTIONS nem APOGEU_PERF_BUDGET_SCALE, quando o limite de 75 ms é resolvido, então é 75 ms, com origem reference machine")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    public void GivenNeitherVariableWhenBudgetIsResolvedThenItIsTheLocalBudget()
    {
        var (budget, source) = PerformanceBudget.Resolve(Local, Ci, Environment());

        Assert.Equal(TimeSpan.FromMilliseconds(75), budget);
        Assert.Equal("reference machine", source);
    }

    [Fact(DisplayName = "Dado só APOGEU_PERF_BUDGET_SCALE=5, quando o limite de 75 ms é resolvido, então é 375 ms, com origem APOGEU_PERF_BUDGET_SCALE=5")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    public void GivenOnlyTheScaleVariableWhenBudgetIsResolvedThenItIsTheLocalBudgetTimesTheFactor()
    {
        var environment = Environment((PerformanceBudget.ScaleVariable, "5"));

        var (budget, source) = PerformanceBudget.Resolve(Local, Ci, environment);

        Assert.Equal(TimeSpan.FromMilliseconds(375), budget);
        Assert.Equal("APOGEU_PERF_BUDGET_SCALE=5", source);
    }

    [Fact(DisplayName = "Dado APOGEU_PERF_BUDGET_SCALE=1.5, quando o limite de 75 ms é resolvido, então é 112,5 ms")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    public void GivenADecimalScaleWhenBudgetIsResolvedThenItIsTheLocalBudgetTimesTheFactor()
    {
        var environment = Environment((PerformanceBudget.ScaleVariable, "1.5"));

        var (budget, _) = PerformanceBudget.Resolve(Local, Ci, environment);

        Assert.Equal(TimeSpan.FromMilliseconds(112.5), budget);
    }

    [Theory(DisplayName = "Dado APOGEU_PERF_BUDGET_SCALE inválido (não numérico ou ≤ 0), quando o limite é resolvido, então lança InvalidOperationException que nomeia a variável e o valor")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public void GivenInvalidScaleWhenBudgetIsResolvedThenExceptionNamesTheVariableAndTheValue(string scale)
    {
        var environment = Environment((PerformanceBudget.ScaleVariable, scale));

        var exception = Assert.Throws<InvalidOperationException>(() => PerformanceBudget.Resolve(Local, Ci, environment));

        Assert.Contains("APOGEU_PERF_BUDGET_SCALE", exception.Message, StringComparison.Ordinal);
        Assert.Contains(scale, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A lookup over the given variables only; absent or <c>null</c> values read as unset.</summary>
    private static Func<string, string?> Environment(params (string Name, string? Value)[] variables)
    {
        var values = variables.Where(v => v.Value is not null).ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return name => values.GetValueOrDefault(name);
    }
}
