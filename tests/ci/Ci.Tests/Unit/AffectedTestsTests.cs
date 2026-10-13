using Apogeu.Ci.Tests.Support;
using Xunit;
using static Apogeu.Ci.Tests.Support.AffectedTests;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>
/// SPEC-0051 (Emenda v2): tools/ci/affected-tests maps the changed files to the test targets the local gates run (a test
/// project, the frontend suite or the whole solution); any backend source, linked test support or contract generator runs
/// the whole solution, and in doubt it runs the whole solution.
/// </summary>
public sealed class AffectedTestsTests
{
    public static TheoryData<string, string[], string[]> ChangedFileSets => new()
    {
        { "só CSS do front", ["src/frontend/apps/game-pipa/src/styles.css"], [Frontend] },
        { "módulo Betting", ["src/backend/Modules/Betting/Betting.Features/BettingModule.cs"], [Solution] },
        {
            "módulo Operators",
            ["src/backend/Modules/Operators/Operators.Features/Authentication/OperatorSignatureAuthenticationHandler.cs"],
            [Solution]
        },
        { "Fairness.Domain", ["src/backend/Modules/Fairness/Fairness.Domain/CrashPointCalculator.cs"], [Solution] },
        { "BuildingBlocks", ["src/backend/BuildingBlocks/Messaging/Outbox.cs"], [Solution] },
        { "Directory.Packages.props", ["Directory.Packages.props"], [Solution] },
        { "só docs", ["docs/specs/x.md"], [] },
        { "só um teste do Wallet", ["tests/backend/Modules/Wallet.Tests/Foo.cs"], [WalletTests] },
        { "Support linkado do Repository.Tests", ["tests/backend/Repository.Tests/Support/ProcessRunner.cs"], [Solution] },
        { "Support de um teste de módulo", ["tests/backend/Modules/Rounds.Tests/Support/Eventually.cs"], [Solution] },
        { "tools/contracts", ["tools/contracts/hubs-to-ts.mjs"], [Solution] },
        { "tsconfig do front", ["src/frontend/tsconfig.base.json"], [Frontend, RepositoryTests] },
        { "eslint do front", ["src/frontend/eslint.config.mjs"], [Frontend, RepositoryTests] },
        { "package.json do front", ["src/frontend/package.json"], [Frontend, RepositoryTests] },
        { "script de CI", ["tools/ci/check-licenses"], [CiTests] },
        { "ferramenta SDD", ["tools/sdd/spec_graph.py"], [CiTests] },
        { "arquivo não mapeado", [".githooks/commit-msg"], [Solution] },
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
