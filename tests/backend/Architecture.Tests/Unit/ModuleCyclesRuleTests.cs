using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class ModuleCyclesRuleTests
{
    [Fact(DisplayName = "Dado um assembly-fixture com ciclo entre Betting e Wallet mediado por Contracts, quando a regra de ciclos roda, então reprova citando os dois módulos")]
    [Trait("Spec", "SPEC-0003:UT-01")]
    [Trait("Category", "Architecture")]
    public void GivenContractsMediatedCycleBetweenBettingAndWalletWhenCycleRuleRunsThenItFailsCitingBothModules()
    {
        // Betting.Features -> Wallet.Contracts (PlaceBetBalanceCheck) and Wallet.Features -> Betting.Contracts
        // (SettlementListener): each edge is allowed by ModuleBoundaries, together they are a module cycle (ADR-0001).
        var violations = RuleViolations.Of(ArchitectureRules.NoCyclesBetweenModules(), FixtureArchitecture.Instance);
        var report = RuleViolations.Describe(violations);

        Assert.True(violations.Count > 0, "Expected the module cycle rule (ADR-0001) to fail on the fixture assembly.");
        Assert.True(
            violations.Any(violation => violation.Contains("Betting", StringComparison.Ordinal)
                && violation.Contains("Wallet", StringComparison.Ordinal)),
            $"Expected a violation citing the Betting and Wallet modules. Violations:{Environment.NewLine}{report}");
    }
}
