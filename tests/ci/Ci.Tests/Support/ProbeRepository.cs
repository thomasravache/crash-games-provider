using System.Globalization;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Disposable clone of <see cref="CiLive.Repository"/> at the tip of the base branch, with git authenticated by the
/// GH_TOKEN of the environment through an inline credential helper (the token never reaches the command line).
/// </summary>
public sealed class ProbeRepository : IAsyncDisposable
{
    private const string RemoteUrl = "https://github.com/" + CiLive.Repository + ".git";

    private ProbeRepository(string workTree, string testId)
    {
        WorkTree = workTree;
        TestId = testId;
        Branch = string.Create(
            CultureInfo.InvariantCulture,
            $"ci-live/{testId.ToLowerInvariant()}-{Guid.NewGuid().ToString("N")[..8]}");
    }

    public string WorkTree { get; }

    public string TestId { get; }

    /// <summary>Disposable branch name used for the probe (ci-live/&lt;id&gt;-&lt;random&gt;).</summary>
    public string Branch { get; }

    public static async Task<ProbeRepository> CloneAsync(string testId, CancellationToken cancellationToken)
    {
        var workTree = Path.Combine(Path.GetTempPath(), "apogeu-ci-live-" + Guid.NewGuid().ToString("N"));
        var clone = await GitAsync(
                Path.GetTempPath(),
                ["clone", "--quiet", "--no-tags", "--single-branch", "--branch", CiLive.BaseBranch, "--depth", "1", RemoteUrl, workTree],
                cancellationToken)
            .ConfigureAwait(false);
        Assert.True(clone.Succeeded, "Setup failed: clone. " + clone.Describe());

        var probe = new ProbeRepository(workTree, testId);
        await probe.GitOrFailAsync(["checkout", "--quiet", "-b", probe.Branch], cancellationToken).ConfigureAwait(false);
        return probe;
    }

    public string PathOf(string relativePath) => Path.Combine([WorkTree, .. relativePath.Split('/')]);

    public void WriteFile(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void AppendToFile(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Assert.True(File.Exists(path), $"Setup failed: {relativePath} not found on {CiLive.BaseBranch}.");
        File.AppendAllText(path, content);
    }

    /// <summary>Commits every change (or an empty commit) with a [no-spec] probe message.</summary>
    public async Task CommitAsync(bool allowEmpty, CancellationToken cancellationToken)
    {
        await GitOrFailAsync(["add", "-A"], cancellationToken).ConfigureAwait(false);
        List<string> arguments = ["commit", "--quiet", "--no-verify", "-m", $"test(ci): ci-live probe {TestId} [no-spec]"];
        if (allowEmpty)
        {
            arguments.Add("--allow-empty");
        }

        await GitOrFailAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    public Task<ProcessResult> PushAsync(string refSpec, CancellationToken cancellationToken) =>
        GitAsync(WorkTree, ["push", "--quiet", "origin", refSpec], cancellationToken);

    public async Task<string> HeadAsync(CancellationToken cancellationToken)
    {
        var head = await GitOrFailAsync(["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false);
        return head.StandardOutput.Trim();
    }

    /// <summary>SHA of a branch on GitHub right now.</summary>
    public async Task<string> RemoteHeadAsync(string branch, CancellationToken cancellationToken)
    {
        var remote = await GitOrFailAsync(["ls-remote", "origin", "refs/heads/" + branch], cancellationToken).ConfigureAwait(false);
        return remote.StandardOutput.Split('\t', 2)[0].Trim();
    }

    public async Task<ProcessResult> GitOrFailAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var result = await GitAsync(WorkTree, arguments, cancellationToken).ConfigureAwait(false);
        Assert.True(result.Succeeded, "git failed. " + result.Describe());
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        // Best effort: the branch may already be gone (deleted on PR close/merge) or never pushed.
        await GitAsync(WorkTree, ["push", "--quiet", "origin", "--delete", Branch], CancellationToken.None).ConfigureAwait(false);
        try
        {
            Directory.Delete(WorkTree, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Task<ProcessResult> GitAsync(string workingDirectory, IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        ProcessRunner.GitAsync(
            workingDirectory,
            [
                "-c", "credential.helper=",
                "-c", "credential.helper=!f() { echo username=x-access-token; echo \"password=$GH_TOKEN\"; }; f",
                "-c", "user.name=Apogeu CI live tests",
                "-c", "user.email=ci-live@users.noreply.github.com",
                "-c", "core.hooksPath=/dev/null",
                .. arguments,
            ],
            cancellationToken);
}
