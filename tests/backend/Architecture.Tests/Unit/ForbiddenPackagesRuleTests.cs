using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class ForbiddenPackagesRuleTests
{
    private const string ViolatingType = "Apogeu.Betting.Features.CashOut.CashOutEndpoint";

    [Fact(DisplayName = "Dado um assembly-fixture que referencia MediatR, quando a regra de pacotes proibidos roda, então reprova")]
    [Trait("Spec", "SPEC-0003:UT-02")]
    [Trait("Category", "Architecture")]
    public void GivenFixtureReferencingMediatRWhenForbiddenPackagesRuleRunsThenItFails()
    {
        var violations = RuleViolations.Of(ArchitectureRules.ForbiddenPackages(), FixtureArchitecture.Instance);
        var report = RuleViolations.Describe(violations);

        Assert.True(violations.Count > 0, "Expected the forbidden packages rule (ADR-0002) to fail on the fixture assembly.");
        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)
                && violation.Contains("MediatR", StringComparison.Ordinal)),
            $"Expected a violation citing {ViolatingType} and MediatR. Violations:{Environment.NewLine}{report}");
    }
}
