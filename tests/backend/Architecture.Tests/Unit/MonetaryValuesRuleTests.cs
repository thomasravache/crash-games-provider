using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class MonetaryValuesRuleTests
{
    private const string ViolatingType = "Apogeu.Betting.Domain.FloatingStake";
    private const string AllowedType = "Apogeu.Betting.Domain.DecimalStake";

    [Fact(DisplayName = "Dada uma classe-fixture com propriedade monetária double, quando a regra de dinheiro roda, então reprova")]
    [Trait("Spec", "SPEC-0003:UT-04")]
    [Trait("Category", "Architecture")]
    public void GivenFixtureWithDoubleMonetaryPropertyWhenMoneyRuleRunsThenItFails()
    {
        var violations = RuleViolations.Of(ArchitectureRules.MonetaryValuesAreDecimal(), FixtureArchitecture.Instance);
        var report = RuleViolations.Describe(violations);

        Assert.True(violations.Count > 0, "Expected the monetary values rule (ADR-0002) to fail on the fixture assembly.");
        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)),
            $"Expected a violation citing {ViolatingType} (double Amount). Violations:{Environment.NewLine}{report}");

        // Control: a decimal monetary property is compliant, so that type must not be reported.
        Assert.False(
            violations.Any(violation => violation.Contains(AllowedType, StringComparison.Ordinal)),
            $"{AllowedType} uses decimal and must not be reported. Violations:{Environment.NewLine}{report}");
    }
}
