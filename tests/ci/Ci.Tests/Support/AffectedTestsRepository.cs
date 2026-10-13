using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Temporary git repository for tools/ci/affected-tests (SPEC-0051 IT-01), deleted on dispose: a branch <c>base</c> with
/// an initial commit holding the script (copied from the repository), one Betting module file and
/// <c>Directory.Packages.props</c>, and the working branch <c>main</c> checked out at the same commit.
/// </summary>
public sealed class AffectedTestsRepository : IDisposable
{
    public const string BaseBranch = "base";
    public const string BettingFile = "src/backend/Modules/Betting/Betting.Domain/Bet.cs";
    public const string PackagesProps = "Directory.Packages.props";

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
        File.AppendAllText(Combine(relativePath), "// changed\n");
        await CommitAllAsync("change " + relativePath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs the copied script from the repository root.</summary>
    public Task<ProcessResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        AffectedTests.RunAsync(Path, Combine(AffectedTests.Script), arguments, cancellationToken);

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
