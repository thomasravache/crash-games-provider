using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Temporary git repository for tools/ci/affected-tests (SPEC-0051 IT-01, IT-02), deleted on dispose: a branch
/// <c>base</c> with an initial commit holding the script (copied from the repository), one Betting module file, one Host
/// file, one frontend file, one Wallet test file and <c>Directory.Packages.props</c>, and the working branch <c>main</c>
/// checked out at the same commit.
/// </summary>
public sealed class AffectedTestsRepository : IDisposable
{
    public const string BaseBranch = "base";
    public const string BettingFile = "src/backend/Modules/Betting/Betting.Domain/Bet.cs";
    public const string PackagesProps = "Directory.Packages.props";
    public const string HostFile = "src/backend/Host/Foo.cs";
    public const string FrontendFile = "src/frontend/apps/game-pipa/src/styles.css";
    public const string RenamedHostFile = "src/frontend/apps/game-pipa/src/Foo.ts";
    public const string WalletTestFile = "tests/backend/Modules/Wallet.Tests/Foo.cs";
    public const string NewWalletTestFile = "tests/backend/Modules/Wallet.Tests/Bar.cs";
    public const string DocsFile = "docs/specs/x.md";

    private AffectedTestsRepository(string path) => Path = path;

    public string Path { get; }

    public static async Task<AffectedTestsRepository> CreateAsync(CancellationToken cancellationToken)
    {
        var source = RepositoryRoot.Combine("tools", "ci", "affected-tests");
        Assert.True(File.Exists(source), $"{AffectedTests.Script} does not exist in the repository ({source}).");

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apogeu-ci-affected-" + Guid.NewGuid().ToString("N"));
        var repository = new AffectedTestsRepository(path);
        try
        {
            Directory.CreateDirectory(path);
            repository.Write(AffectedTests.Script, File.ReadAllText(source));
            repository.Write(BettingFile, "namespace Apogeu.Betting.Domain;\n\npublic sealed class Bet;\n");
            repository.Write(PackagesProps, "<Project>\n</Project>\n");
            repository.Write(HostFile, "namespace Apogeu.Host;\n\npublic sealed class Foo;\n");
            repository.Write(FrontendFile, "body {\n  margin: 0;\n}\n");
            repository.Write(WalletTestFile, "namespace Apogeu.Wallet.Tests;\n\npublic sealed class Foo;\n");

            await repository.GitAsync(["init", "--quiet", "--initial-branch=main"], cancellationToken).ConfigureAwait(false);
            await repository.GitAsync(["config", "user.name", "Apogeu Tests"], cancellationToken).ConfigureAwait(false);
            await repository.GitAsync(["config", "user.email", "tests@apogeu.invalid"], cancellationToken).ConfigureAwait(false);
            await repository.GitAsync(["config", "commit.gpgsign", "false"], cancellationToken).ConfigureAwait(false);
            await repository.CommitAllAsync("initial", cancellationToken).ConfigureAwait(false);
            await repository.GitAsync(["branch", BaseBranch], cancellationToken).ConfigureAwait(false);
            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    /// <summary>Appends a line to <paramref name="relativePath"/> and commits it on the current branch.</summary>
    public async Task CommitChangeAsync(string relativePath, CancellationToken cancellationToken)
    {
        Change(relativePath);
        await CommitAllAsync("change " + relativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends a line to <paramref name="relativePath"/> (created when missing) without committing it.</summary>
    public void Change(string relativePath)
    {
        var file = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.AppendAllText(file, "// changed\n");
    }

    /// <summary>Renames <paramref name="from"/> to <paramref name="to"/> with <c>git mv</c> and commits it.</summary>
    public async Task CommitMoveAsync(string from, string to, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Combine(to))!);
        await GitAsync(["mv", from, to], cancellationToken).ConfigureAwait(false);
        await GitAsync(["commit", "--quiet", "--no-verify", "-m", $"move {from} to {to}"], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The SHA of <c>HEAD</c>.</summary>
    public async Task<string> HeadAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.GitAsync(Path, ["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false);
        Assert.True(result.Succeeded, "git rev-parse HEAD failed. " + result.Describe());
        return result.StandardOutput.Trim();
    }

    /// <summary>Runs the copied script from the repository root.</summary>
    public Task<ProcessResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        AffectedTests.RunAsync(Path, Combine(AffectedTests.Script), arguments, cancellationToken);

    /// <summary>Runs the copied script from the repository root with extra environment variables.</summary>
    public Task<ProcessResult> RunAsync(
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken) =>
        AffectedTests.RunAsync(Path, Combine(AffectedTests.Script), arguments, environment, cancellationToken);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort: read-only git objects may resist deletion on some platforms.
        }
    }

    private async Task CommitAllAsync(string message, CancellationToken cancellationToken)
    {
        await GitAsync(["add", "--all"], cancellationToken).ConfigureAwait(false);
        await GitAsync(["commit", "--quiet", "--no-verify", "-m", message], cancellationToken).ConfigureAwait(false);
    }

    private async Task GitAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.GitAsync(Path, arguments, cancellationToken).ConfigureAwait(false);
        Assert.True(result.Succeeded, "git failed while preparing the temporary repository. " + result.Describe());
    }

    private void Write(string relativePath, string content)
    {
        var file = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private string Combine(string relativePath) =>
        System.IO.Path.Combine([Path, .. relativePath.Split('/')]);
}
