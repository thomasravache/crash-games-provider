using Apogeu.Ci.Tests.Support;
using Xunit;
using static Apogeu.Ci.Tests.Support.AffectedTests;

namespace Apogeu.Ci.Tests.Integration;

/// <summary>
/// IT-01 of SPEC-0051: in a temporary git repository, tools/ci/affected-tests reads the changed files from
/// <c>git diff base...HEAD</c> and, with <c>--dry-run</c>, prints only the commands of the selected targets.
/// </summary>
public sealed class AffectedTestsGitTests
{
    [Fact(DisplayName = "Dado um commit que altera só um arquivo do módulo Betting, quando tools/ci/affected-tests roda com --dry-run, então imprime só os comandos de Betting.Tests e Architecture.Tests e sai com 0")]
    [Trait("Spec", "SPEC-0051:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenBettingOnlyCommitWhenAffectedTestsDryRunsThenItPrintsOnlyBettingAndArchitectureCommands()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        await repository.CommitChangeAsync(AffectedTestsRepository.BettingFile, cancellationToken);

        var result = await repository.RunAsync(["--base", AffectedTestsRepository.BaseBranch, "--dry-run"], cancellationToken);

        AssertCommands(result, [.. TestCommands(BettingTests), .. TestCommands(ArchitectureTests)]);
    }

    [Fact(DisplayName = "Dado um commit que altera Directory.Packages.props, quando tools/ci/affected-tests roda com --dry-run, então imprime só os comandos da solução inteira e sai com 0")]
    [Trait("Spec", "SPEC-0051:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenPackagesPropsCommitWhenAffectedTestsDryRunsThenItPrintsOnlySolutionCommands()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        await repository.CommitChangeAsync(AffectedTestsRepository.PackagesProps, cancellationToken);

        var result = await repository.RunAsync(["--base", AffectedTestsRepository.BaseBranch, "--dry-run"], cancellationToken);

        AssertCommands(result, TestCommands("Apogeu.slnx"));
    }

    private static void AssertCommands(Repository.Tests.Support.ProcessResult result, IEnumerable<string> expected)
    {
        Assert.True(result.Succeeded, "affected-tests --dry-run should exit with code 0. " + result.Describe());
        var commands = Lines(result);
        var sortedExpected = expected.Order(StringComparer.Ordinal).ToList();
        Assert.True(
            commands.SequenceEqual(sortedExpected, StringComparer.Ordinal),
            $"Expected exactly:{Environment.NewLine}{string.Join(Environment.NewLine, sortedExpected)}{Environment.NewLine}"
            + $"but got:{Environment.NewLine}{string.Join(Environment.NewLine, commands)}{Environment.NewLine}" + result.Describe());
    }
}
