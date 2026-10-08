using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Integration;

// Guards (IT-01): the rules of All() that have no UT of their own must also detect a known violation, otherwise their
// green result on the real assemblies proves nothing.
public sealed class DomainPurityGuardTests
{
    [Fact(DisplayName = "Dado um assembly-fixture com Rounds.Domain usando ASP.NET Core, quando a regra DomainIsFrameworkFree roda, então reprova")]
    [Trait("Spec", "SPEC-0003:IT-01")]
    [Trait("Category", "Architecture")]
    public void GivenRoundsDomainUsingAspNetCoreWhenFrameworkFreeRuleRunsThenItFails()
    {
        var violations = RuleViolations.Of(ArchitectureRules.DomainIsFrameworkFree(), FixtureArchitecture.Instance);

        Assert.True(
            violations.Any(violation => violation.Contains("Apogeu.Rounds.Domain.RoundHttpProbe", StringComparison.Ordinal)
                && violation.Contains("Microsoft.AspNetCore.Http", StringComparison.Ordinal)),
            "Expected a violation citing RoundHttpProbe and Microsoft.AspNetCore.Http. Violations:"
            + Environment.NewLine + RuleViolations.Describe(violations));
    }

    [Theory(DisplayName = "Dado um assembly-fixture com Fairness.Domain fazendo I/O ou lendo o relógio, quando a regra FairnessDomainIsPure roda, então reprova")]
    [Trait("Spec", "SPEC-0003:IT-01")]
    [Trait("Category", "Architecture")]
    [InlineData("Apogeu.Fairness.Domain.SeedFileStore", "System.IO.File")]
    [InlineData("Apogeu.Fairness.Domain.SeedClock", "System.DateTime")]
    public void GivenFairnessDomainWithInfrastructureWhenPureRuleRunsThenItFails(string violatingType, string target)
    {
        var violations = RuleViolations.Of(ArchitectureRules.FairnessDomainIsPure(), FixtureArchitecture.Instance);

        Assert.True(
            violations.Any(violation => violation.Contains(violatingType, StringComparison.Ordinal)
                && violation.Contains(target, StringComparison.Ordinal)),
            $"Expected a violation citing {violatingType} and {target}. Violations:"
            + Environment.NewLine + RuleViolations.Describe(violations));
    }
}
