using Apogeu.Betting.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>The local p99 budget of the cash-out burst (SPEC-0052 §3), resolved from a given environment, never from the real one.</summary>
public sealed class CashOutLatencyBudgetTests
{
    [Theory(DisplayName = "Dado GITHUB_ACTIONS=true, com ou sem APOGEU_CASHOUT_P99_BUDGET_MS=400, quando o limite é resolvido, então é 3 s")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    [InlineData(null)]
    [InlineData("400")]
    public void GivenGitHubActionsWhenBudgetIsResolvedThenItIsThreeSeconds(string? budget)
    {
        var environment = Environment(("GITHUB_ACTIONS", "true"), (CashOutLatencyBudget.Variable, budget));

        Assert.Equal(TimeSpan.FromSeconds(3), CashOutLatencyBudget.Resolve(environment));
    }

    [Fact(DisplayName = "Dado um ambiente sem GITHUB_ACTIONS nem APOGEU_CASHOUT_P99_BUDGET_MS, quando o limite é resolvido, então é 75 ms")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    public void GivenNeitherVariableWhenBudgetIsResolvedThenItIsSeventyFiveMilliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(75), CashOutLatencyBudget.Resolve(Environment()));
    }

    [Fact(DisplayName = "Dado só APOGEU_CASHOUT_P99_BUDGET_MS=400, quando o limite é resolvido, então é 400 ms")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    public void GivenOnlyTheBudgetVariableWhenBudgetIsResolvedThenItIsItsValue()
    {
        var environment = Environment((CashOutLatencyBudget.Variable, "400"));

        Assert.Equal(TimeSpan.FromMilliseconds(400), CashOutLatencyBudget.Resolve(environment));
    }

    [Theory(DisplayName = "Dado APOGEU_CASHOUT_P99_BUDGET_MS inválido (não inteiro ou ≤ 0), quando o limite é resolvido, então lança exceção que nomeia a variável")]
    [Trait("Spec", "SPEC-0052:UT-01")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void GivenInvalidBudgetVariableWhenBudgetIsResolvedThenExceptionNamesTheVariable(string budget)
    {
        var environment = Environment((CashOutLatencyBudget.Variable, budget));

        var exception = Assert.ThrowsAny<Exception>(() => CashOutLatencyBudget.Resolve(environment));

        Assert.IsNotType<NotImplementedException>(exception);
        Assert.Contains("APOGEU_CASHOUT_P99_BUDGET_MS", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A lookup over the given variables only; absent or <c>null</c> values read as unset.</summary>
    private static Func<string, string?> Environment(params (string Name, string? Value)[] variables)
    {
        var values = variables.Where(v => v.Value is not null).ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return name => values.GetValueOrDefault(name);
    }
}
