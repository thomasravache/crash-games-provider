using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class ForbiddenPackagesHiddenDependencyTests
{
    [Fact(DisplayName = "Dado o Host chamando AddAutoMapper (namespace Microsoft.Extensions.DependencyInjection, assembly AutoMapper), quando a regra de pacotes proibidos roda, então reprova")]
    [Trait("Spec", "SPEC-0003:UT-02")]
    [Trait("Category", "Architecture")]
    public void GivenHostCallingAutoMapperExtensionWhenForbiddenPackagesRuleRunsThenItFails()
    {
        const string ViolatingType = "Apogeu.Host.HostComposition";

        var violations = RuleViolations.Of(ArchitectureRules.ForbiddenPackages(), FixtureArchitecture.Instance);

        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)),
            $"Expected a violation citing {ViolatingType}: the AutoMapper extension is identified by its assembly, not "
            + $"its namespace. Violations:{Environment.NewLine}{RuleViolations.Describe(violations)}");
    }

    [Fact(DisplayName = "Dado um tipo no namespace global (estilo Program de top-level) usando MediatR, quando a regra de pacotes proibidos roda, então reprova")]
    [Trait("Spec", "SPEC-0003:UT-02")]
    [Trait("Category", "Architecture")]
    public void GivenGlobalNamespaceTypeUsingMediatRWhenForbiddenPackagesRuleRunsThenItFails()
    {
        const string ViolatingType = "FixtureEntryPoint";

        var violations = RuleViolations.Of(ArchitectureRules.ForbiddenPackages(), FixtureArchitecture.Instance);

        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)
                && violation.Contains("MediatR", StringComparison.Ordinal)),
            $"Expected a violation citing the global-namespace type {ViolatingType} and MediatR. "
            + $"Violations:{Environment.NewLine}{RuleViolations.Describe(violations)}");
    }
}
