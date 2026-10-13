using Apogeu.Ci.Tests.Support;
using Xunit;
using static Apogeu.Ci.Tests.Support.AffectedTests;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>
/// SPEC-0051: tools/ci/affected-tests maps the changed files to the test targets the local gates run (a test project,
/// the frontend suite or the whole solution), and in doubt runs the whole solution.
/// </summary>
public sealed class AffectedTestsTests
{
    public static TheoryData<string, string[], string[]> ChangedFileSets => new()
    {
        { "só CSS do front", ["src/frontend/apps/game-pipa/src/styles.css"], [Frontend] },
        { "só o módulo Betting", ["src/backend/Modules/Betting/Betting.Features/BettingModule.cs"], [ArchitectureTests, BettingTests] },
        {
            "Betting + Rounds",
            ["src/backend/Modules/Betting/Betting.Domain/Bet.cs", "src/backend/Modules/Rounds/Rounds.Domain/GrowthCurve.cs"],
            [ArchitectureTests, BettingTests, RoundsTests]
        },
        { "BuildingBlocks", ["src/backend/BuildingBlocks/Messaging/Outbox.cs"], [Solution] },
        { "Directory.Packages.props", ["Directory.Packages.props"], [Solution] },
        { "só docs", ["docs/specs/x.md"], [] },
        { "só um teste", ["tests/backend/Modules/Wallet.Tests/Foo.cs"], [WalletTests] },
        { "arquivo não mapeado", [".githooks/commit-msg"], [Solution] },
        { "contrato de módulo", ["src/backend/Modules/Betting/Betting.Contracts/BetPlaced.cs"], [Solution] },
        { "script de CI", ["tools/ci/check-licenses"], [CiTests] },
    };

    [Theory(DisplayName = "Dado um conjunto de arquivos alterados, quando tools/ci/affected-tests lista os alvos, então devolve exatamente os alvos esperados")]
    [Trait("Spec", "SPEC-0051:UT-01")]
    [MemberData(nameof(ChangedFileSets))]
    public async Task GivenChangedFilesWhenAffectedTestsListsTargetsThenItReturnsExactlyTheExpectedTargets(
        string scenario,
        string[] files,
        string[] expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await ListAsync(files, cancellationToken);

        Assert.True(result.Succeeded, $"affected-tests --list failed ({scenario}). " + result.Describe());
        var targets = Lines(result);
        Assert.True(
            targets.SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            $"Scenario '{scenario}': expected [{string.Join(", ", expected)}] but got [{string.Join(", ", targets)}]. " + result.Describe());
    }
}
