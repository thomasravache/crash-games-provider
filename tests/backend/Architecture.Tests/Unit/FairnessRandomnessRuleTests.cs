using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class FairnessRandomnessRuleTests
{
    private const string ViolatingType = "Apogeu.Fairness.Domain.InsecureServerSeedGenerator";
    private const string AllowedType = "Apogeu.Fairness.Domain.CryptographicServerSeedGenerator";

    [Fact(DisplayName = "Dado um assembly-fixture de Fairness usando System.Random, quando a regra roda, então reprova")]
    [Trait("Spec", "SPEC-0003:UT-03")]
    [Trait("Category", "Architecture")]
    public void GivenFairnessFixtureUsingSystemRandomWhenRuleRunsThenItFails()
    {
        var violations = RuleViolations.Of(
            ArchitectureRules.FairnessUsesCryptographicRandomness(),
            FixtureArchitecture.Instance);
        var report = RuleViolations.Describe(violations);

        Assert.True(violations.Count > 0, "Expected the Fairness randomness rule (ADR-0006) to fail on the fixture assembly.");
        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)
                && violation.Contains("System.Random", StringComparison.Ordinal)),
            $"Expected a violation citing {ViolatingType} and System.Random. Violations:{Environment.NewLine}{report}");

        // Control: RandomNumberGenerator is the required source, so that type must not be reported.
        Assert.False(
            violations.Any(violation => violation.Contains(AllowedType, StringComparison.Ordinal)),
            $"{AllowedType} uses RandomNumberGenerator and must not be reported. Violations:{Environment.NewLine}{report}");
    }
}
