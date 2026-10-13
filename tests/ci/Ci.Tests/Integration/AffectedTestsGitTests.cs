using Apogeu.Ci.Tests.Support;
using Xunit;
using static Apogeu.Ci.Tests.Support.AffectedTests;

namespace Apogeu.Ci.Tests.Integration;

/// <summary>
/// IT-01 and IT-02 of SPEC-0051 (Emenda v2): in a temporary git repository, tools/ci/affected-tests reads the changed
/// files from <c>git diff --no-renames base...HEAD</c> plus the uncommitted and untracked files; with <c>--dry-run</c> it
/// prints only the commands of the selected targets, without it it runs them and returns the first failure's exit code.
/// </summary>
public sealed class AffectedTestsGitTests
{
    private const string SolutionFile = "Apogeu.slnx";

    [Fact(DisplayName = "Dado um commit que altera só um arquivo do front, quando tools/ci/affected-tests roda com --dry-run, então imprime só pnpm -C src/frontend test e sai com 0")]
    [Trait("Spec", "SPEC-0051:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenFrontendOnlyCommitWhenAffectedTestsDryRunsThenItPrintsOnlyTheFrontendCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        await repository.CommitChangeAsync(AffectedTestsRepository.FrontendFile, cancellationToken);

        var result = await repository.RunAsync(["--base", AffectedTestsRepository.BaseBranch, "--dry-run"], cancellationToken);

        AssertCommands(result, ["pnpm -C src/frontend test"]);
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

        AssertCommands(result, TestCommands(SolutionFile));
    }

    [Fact(DisplayName = "Dado um commit que renomeia com git mv um arquivo de src/backend/Host para src/frontend, quando tools/ci/affected-tests roda com --dry-run, então imprime os comandos da solução inteira (o caminho antigo conta)")]
    [Trait("Spec", "SPEC-0051:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRenameFromBackendToFrontendWhenAffectedTestsDryRunsThenItPrintsSolutionCommands()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        await repository.CommitMoveAsync(AffectedTestsRepository.HostFile, AffectedTestsRepository.RenamedHostFile, cancellationToken);

        var result = await repository.RunAsync(["--base", AffectedTestsRepository.BaseBranch, "--dry-run"], cancellationToken);

        AssertCommands(result, TestCommands(SolutionFile));
    }

    [Fact(DisplayName = "Dado uma alteração não commitada e um arquivo não rastreado num teste do Wallet e dotnet/pnpm falsos no PATH, quando tools/ci/affected-tests roda sem --dry-run, então executa só os comandos do Wallet.Tests e sai com 0")]
    [Trait("Spec", "SPEC-0051:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenUncommittedWalletTestChangesWhenAffectedTestsRunsThenItRunsOnlyWalletCommands()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var tools = FakeToolchain.Create();
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        repository.Change(AffectedTestsRepository.WalletTestFile);
        repository.Change(AffectedTestsRepository.NewWalletTestFile);

        var result = await repository.RunAsync(["--base", AffectedTestsRepository.BaseBranch], tools.Environment(), cancellationToken);

        Assert.True(result.Succeeded, "affected-tests should exit with 0 when every command passes. " + result.Describe());
        AssertCalls(tools, [.. TestCommands(WalletTests).Select(FakeToolchain.AsLogged)], result);
    }

    [Fact(DisplayName = "Dado um teste do Wallet alterado e um dotnet falso que falha com 3, quando tools/ci/affected-tests roda sem --dry-run, então para no primeiro comando e sai com 3")]
    [Trait("Spec", "SPEC-0051:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenFailingDotnetWhenAffectedTestsRunsThenItStopsAtTheFirstCommandWithItsExitCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var tools = FakeToolchain.Create();
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        repository.Change(AffectedTestsRepository.WalletTestFile);
        repository.Change(AffectedTestsRepository.NewWalletTestFile);

        var result = await repository.RunAsync(["--base", AffectedTestsRepository.BaseBranch], tools.Environment(dotnetExit: 3), cancellationToken);

        Assert.True(result.ExitCode == 3, "affected-tests should return the first failing command's exit code (3). " + result.Describe());
        AssertCalls(tools, [FakeToolchain.AsLogged(TestCommands(WalletTests).First())], result);
    }

    [Fact(DisplayName = "Dado AFFECTED_BASE apontando um commit que já contém a alteração do Wallet e depois só um commit de docs, quando tools/ci/affected-tests lista os alvos, então usa essa base e não seleciona nada")]
    [Trait("Spec", "SPEC-0051:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenAffectedBaseEnvironmentWhenAffectedTestsListsThenItUsesThatBase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var repository = await AffectedTestsRepository.CreateAsync(cancellationToken);
        await repository.CommitChangeAsync(AffectedTestsRepository.WalletTestFile, cancellationToken);
        var walletCommit = await repository.HeadAsync(cancellationToken);
        await repository.CommitChangeAsync(AffectedTestsRepository.DocsFile, cancellationToken);

        var fromWalletCommit = await repository.RunAsync(
            ["--list"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["AFFECTED_BASE"] = walletCommit },
            cancellationToken);
        var fromBaseBranch = await repository.RunAsync(
            ["--list"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["AFFECTED_BASE"] = AffectedTestsRepository.BaseBranch },
            cancellationToken);

        Assert.True(fromWalletCommit.Succeeded, "affected-tests --list failed. " + fromWalletCommit.Describe());
        Assert.True(
            Lines(fromWalletCommit).Count == 0,
            "With AFFECTED_BASE at the Wallet commit only docs changed, so nothing should be selected. " + fromWalletCommit.Describe());
        Assert.True(fromBaseBranch.Succeeded, "affected-tests --list failed. " + fromBaseBranch.Describe());
        Assert.True(
            Lines(fromBaseBranch).SequenceEqual([WalletTests], StringComparer.Ordinal),
            $"With AFFECTED_BASE={AffectedTestsRepository.BaseBranch} only Wallet.Tests should be selected. " + fromBaseBranch.Describe());
    }

    private static void AssertCalls(FakeToolchain tools, IReadOnlyList<string> expected, Repository.Tests.Support.ProcessResult result)
    {
        var calls = tools.Calls();
        Assert.True(
            calls.SequenceEqual(expected, StringComparer.Ordinal),
            $"Expected the fake tools to be called exactly:{Environment.NewLine}{string.Join(Environment.NewLine, expected)}{Environment.NewLine}"
            + $"but got:{Environment.NewLine}{string.Join(Environment.NewLine, calls)}{Environment.NewLine}" + result.Describe());
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
