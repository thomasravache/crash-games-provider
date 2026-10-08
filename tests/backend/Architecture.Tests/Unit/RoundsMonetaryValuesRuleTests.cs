using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class RoundsMonetaryValuesRuleTests
{
    private const string ViolatingType = "Apogeu.Rounds.Domain.RoundPayout";
    private const string AllowedType = "Apogeu.Rounds.Domain.CrashCurve";

    [Fact(DisplayName = "Dada uma propriedade de nome monetário double em Rounds.Domain, quando a regra de dinheiro roda, então reprova sem citar o expoente da curva")]
    [Trait("Spec", "SPEC-0003:UT-04")]
    [Trait("Category", "Architecture")]
    public void GivenMoneyNamedDoubleInRoundsDomainWhenMoneyRuleRunsThenItFailsWithoutCitingCurveExponent()
    {
        var violations = RuleViolations.Of(ArchitectureRules.MonetaryValuesAreDecimal(), FixtureArchitecture.Instance);
        var report = RuleViolations.Describe(violations);

        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)),
            $"Expected a violation citing {ViolatingType} (double PayoutAmount). Violations:{Environment.NewLine}{report}");

        // Control: the curve exponent (double GrowthK) in Rounds.Domain is not money.
        Assert.False(
            violations.Any(violation => violation.Contains(AllowedType, StringComparison.Ordinal)
                || violation.Contains("GrowthK", StringComparison.Ordinal)),
            $"{AllowedType}.GrowthK is not money and must not be reported. Violations:{Environment.NewLine}{report}");
    }
}
