using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Integration;

public sealed class SolutionArchitectureTests
{
    private static readonly string[] RequiredAdrs = ["ADR-0001", "ADR-0002", "ADR-0006"];

    [Fact(DisplayName = "Dados os assemblies reais da solução, quando a suíte de arquitetura roda, então todas as regras passam")]
    [Trait("Spec", "SPEC-0003:IT-01")]
    [Trait("Category", "Architecture")]
    public void GivenRealSolutionAssembliesWhenArchitectureSuiteRunsThenEveryRulePasses()
    {
        // The suite must look at every back-end project of the solution, otherwise a green run proves nothing.
        var expected = SolutionArchitecture.ExpectedAssemblyNames();
        Assert.True(expected.Count > 0, "Apogeu.slnx lists no project under src/backend.");
        var missing = expected.Where(name => !File.Exists(SolutionArchitecture.OutputPath(name))).ToList();
        Assert.True(
            missing.Count == 0,
            "Assemblies of the solution missing from the test output (reference them from Architecture.Tests): "
            + string.Join(", ", missing));

        var rules = ArchitectureRules.All();
        var coveredAdrs = rules.Select(rule => rule.Adr).ToHashSet(StringComparer.Ordinal);
        Assert.All(RequiredAdrs, adr => Assert.Contains(adr, coveredAdrs));

        var architecture = SolutionArchitecture.Instance;
        var failures = rules
            .SelectMany(rule => RuleViolations.Of(rule.Rule, architecture)
                .Select(violation => $"[{rule.Adr}] {rule.Name}: {violation}"))
            .ToList();

        Assert.True(
            failures.Count == 0,
            "Architecture rules violated by the real assemblies:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}
