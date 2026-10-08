using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class ModuleBoundariesRuleTests
{
    private const string ViolatingType = "Apogeu.Betting.Features.PlaceBet.PlaceBetHandler";
    private const string ForbiddenTarget = "Apogeu.Wallet.Domain.LedgerEntry";
    private const string AllowedType = "Apogeu.Betting.Features.PlaceBet.PlaceBetBalanceCheck";

    [Fact(DisplayName = "Dado um assembly-fixture em que Betting.Features referencia Wallet.Domain, quando a regra de fronteira roda, então reprova citando os dois tipos")]
    [Trait("Spec", "SPEC-0003:UT-01")]
    [Trait("Category", "Architecture")]
    public void GivenBettingFeaturesReferencingWalletDomainWhenBoundaryRuleRunsThenItFailsCitingBothTypes()
    {
        var violations = RuleViolations.Of(ArchitectureRules.ModuleBoundaries(), FixtureArchitecture.Instance);
        var report = RuleViolations.Describe(violations);

        Assert.True(violations.Count > 0, "Expected the module boundary rule (ADR-0001) to fail on the fixture assembly.");
        Assert.True(
            violations.Any(violation => violation.Contains(ViolatingType, StringComparison.Ordinal)
                && violation.Contains(ForbiddenTarget, StringComparison.Ordinal)),
            $"Expected a violation citing {ViolatingType} and {ForbiddenTarget}. Violations:{Environment.NewLine}{report}");

        // Control: depending on another module's Contracts is allowed, so that type must not be reported.
        Assert.False(
            violations.Any(violation => violation.Contains(AllowedType, StringComparison.Ordinal)),
            $"{AllowedType} only references Wallet.Contracts and must not be reported. Violations:{Environment.NewLine}{report}");
    }
}
