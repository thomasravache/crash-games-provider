using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class MessagingOwnedByHostRuleTests
{
    private const string Prefix = "Apogeu.Betting.Features.Messaging.";

    [Theory(DisplayName = "Dado um assembly-fixture em que um módulo manipula hosted services de outros (descritores, RemoveAll<IHostedService>, IEnumerable<IHostedService>), quando a regra MessagingOwnedByHost roda, então reprova cada um")]
    [Trait("Spec", "SPEC-0022:G3")]
    [Trait("Category", "Architecture")]
    [InlineData("HostedServiceDescriptorEdit")]
    [InlineData("HostedServiceRemoval")]
    [InlineData("HostedServiceLookup")]
    public void GivenModuleManipulatingHostedServicesWhenRuleRunsThenItFails(string violatingType)
    {
        var violations = RuleViolations.Of(ArchitectureRules.MessagingOwnedByHost(), FixtureArchitecture.Instance);

        Assert.True(
            violations.Any(violation => violation.Contains(Prefix + violatingType, StringComparison.Ordinal)),
            $"Expected MessagingOwnedByHost (ADR-0028) to report {Prefix + violatingType}. Violations:{Environment.NewLine}{RuleViolations.Describe(violations)}");
    }

    [Fact(DisplayName = "Dado um módulo que registra e implementa o próprio hosted service, quando a regra MessagingOwnedByHost roda, então não o reprova")]
    [Trait("Spec", "SPEC-0022:G3")]
    [Trait("Category", "Architecture")]
    public void GivenModuleRegisteringItsOwnHostedServiceWhenRuleRunsThenItIsAllowed()
    {
        var violations = RuleViolations.Of(ArchitectureRules.MessagingOwnedByHost(), FixtureArchitecture.Instance);

        Assert.False(
            violations.Any(violation => violation.Contains(Prefix + "OwnHostedServiceRegistration", StringComparison.Ordinal)
                || violation.Contains(Prefix + "OwnWorker", StringComparison.Ordinal)),
            $"A module's own hosted service is allowed. Violations:{Environment.NewLine}{RuleViolations.Describe(violations)}");
    }
}
