using Apogeu.Contracts.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Unit;

/// <summary>
/// Hub-contract generator (HubContracts + <c>tools/contracts/hubs-to-ts.mjs</c>, ADR-0008) proven on the test-only
/// <see cref="FixtureHub"/>: the same scalar and nullability conventions as the REST contracts.
/// </summary>
public sealed class HubContractGenerationTests(FixtureHubGeneration generation) : IClassFixture<FixtureHubGeneration>
{
    private const string Server = nameof(FixtureHub) + "Server";
    private const string Client = nameof(FixtureHub) + "Client";

    [Fact(DisplayName = "Dado um hub com Money e Multiplier?, quando o contrato de hubs é gerado, então o TS usa Money e Multiplier | null")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public async Task GivenHubWithMoneyAndNullableMultiplierWhenHubContractIsGeneratedThenTypeScriptUsesBrandedScalars()
    {
        var typeScript = await generation.TypeScriptAsync();

        Assert.Contains(
            FixtureHubGeneration.Member(typeScript, Server, nameof(FixtureHub.FixtureCashout)),
            (string[])["FixtureCashout(stake: Money): Promise<Multiplier | null>", "FixtureCashout(stake: Money): Promise<null | Multiplier>"],
            StringComparer.Ordinal);
        Assert.Equal(
            "FixtureRoundStarted(stake: Money, target: Multiplier): void",
            FixtureHubGeneration.Member(typeScript, Client, nameof(IFixtureClient.FixtureRoundStarted)));
        Assert.Matches(
            $@"import\s+type\s+\{{\s*Money,\s*Multiplier\s*\}}\s+from\s+""{FixtureHubGeneration.ScalarsFrom}""",
            typeScript);
    }

    [Fact(DisplayName = "Dado um hub com string?, stream e métodos de cliente, quando o contrato de hubs é gerado, então o TS tem string | null, Promise, AsyncIterable e void")]
    [Trait("Spec", "SPEC-0006:UT-02")]
    public async Task GivenHubWithNullableStringStreamAndClientMethodsWhenHubContractIsGeneratedThenTypeScriptMatchesTheWireShape()
    {
        var typeScript = await generation.TypeScriptAsync();

        Assert.Contains(
            FixtureHubGeneration.Member(typeScript, Server, nameof(FixtureHub.FixtureRename)),
            (string[])["FixtureRename(nickname: string | null): Promise<void>", "FixtureRename(nickname: null | string): Promise<void>"],
            StringComparer.Ordinal);
        Assert.Equal(
            "FixtureTicks(stake: Money): AsyncIterable<Multiplier>",
            FixtureHubGeneration.Member(typeScript, Server, nameof(FixtureHub.FixtureTicks)));
        Assert.Contains(
            FixtureHubGeneration.Member(typeScript, Client, nameof(IFixtureClient.FixtureNicknameChanged)),
            (string[])["FixtureNicknameChanged(nickname: string | null): void", "FixtureNicknameChanged(nickname: null | string): void"],
            StringComparer.Ordinal);
        Assert.Matches(@"export\s+const\s+hubNames\s*=\s*\[\s*""FixtureHub""\s*\]\s*as\s+const", typeScript);
    }

    [Fact(DisplayName = "Dado um hub que sobrescreve o ciclo de vida, quando o contrato de hubs é gerado, então OnConnectedAsync, OnDisconnectedAsync e Dispose não aparecem")]
    [Trait("Spec", "SPEC-0006:UT-02")]
    public async Task GivenHubOverridingLifecycleWhenHubContractIsGeneratedThenLifecycleMethodsAreHidden()
    {
        var typeScript = await generation.TypeScriptAsync();

        var server = FixtureHubGeneration.InterfaceMembers(typeScript, Server);
        Assert.Equal(
            [nameof(FixtureHub.FixtureCashout), nameof(FixtureHub.FixtureRename), nameof(FixtureHub.FixtureTicks)],
            server.Select(member => member[..member.IndexOf('(', StringComparison.Ordinal)]),
            StringComparer.Ordinal);
        foreach (var hidden in (string[])["OnConnectedAsync", "OnDisconnectedAsync", "Dispose", "cancellationToken"])
        {
            Assert.DoesNotContain(hidden, typeScript, StringComparison.Ordinal);
        }
    }
}
