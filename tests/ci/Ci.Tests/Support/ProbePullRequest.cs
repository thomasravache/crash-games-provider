using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Pull request opened from a <see cref="ProbeRepository"/> branch against the base branch. Titled
/// "test(ci): … [no-spec]" with no spec reference, so the sdd job (pr-check) accepts it as a no-spec change.
/// Disposing closes it (if still open) and deletes the branch — always, also when the test fails.
/// </summary>
public sealed class ProbePullRequest : IAsyncDisposable
{
    private static readonly TimeSpan JobLogTimeout = TimeSpan.FromMinutes(3);

    private ProbePullRequest(ProbeRepository repository, int number, string url)
    {
        Repository = repository;
        Number = number;
        Url = url;
    }

    public ProbeRepository Repository { get; }

    public int Number { get; }

    public string Url { get; }

    private string NumberText => Number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Pushes the probe branch (already committed) and opens the pull request.</summary>
    public static async Task<ProbePullRequest> OpenAsync(ProbeRepository repository, CancellationToken cancellationToken)
    {
        var push = await repository.PushAsync("HEAD:refs/heads/" + repository.Branch, cancellationToken).ConfigureAwait(false);
        Assert.True(push.Succeeded, "Setup failed: push of the probe branch. " + push.Describe());

        var create = await GitHubCli
            .RunAsync(
                [
                    "pr", "create",
                    "--base", CiLive.BaseBranch,
                    "--head", repository.Branch,
                    "--title", $"test(ci): ci-live probe {repository.TestId} [no-spec]",
                    "--body", "Disposable pull request opened by the live CI test suite (tests/ci). It is closed and its branch deleted automatically.",
                ],
                cancellationToken)
            .ConfigureAwait(false);
        Assert.True(create.Succeeded, "Setup failed: gh pr create. " + create.Describe());

        var url = create.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
        var number = int.Parse(url[(url.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);
        return new ProbePullRequest(repository, number, url);
    }

    /// <summary>
    /// Polls the checks until every expected job of <paramref name="workflow"/> is reported and none of its checks is
    /// pending (equivalent to <c>gh pr checks --watch</c>, but tolerant to checks that are not registered yet).
    /// </summary>
    public async Task<IReadOnlyList<PullRequestCheck>> WaitForChecksAsync(
        string workflow,
        IReadOnlyCollection<string> expectedJobs,
        CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        IReadOnlyList<PullRequestCheck> checks = [];
        while (clock.Elapsed < CiLive.ChecksTimeout)
        {
            var result = await GitHubCli
                .RunAsync(["pr", "checks", NumberText, "--json", PullRequestCheck.Fields], cancellationToken)
                .ConfigureAwait(false);
            var output = result.StandardOutput.Trim();
            if (output.StartsWith('['))
            {
                using var document = JsonDocument.Parse(output);
                checks = PullRequestCheck.ParseAll(document.RootElement);
                var ofWorkflow = checks.Where(check => expectedJobs.Any(job => check.Is(workflow, job))).ToList();
                var allReported = expectedJobs.All(job => ofWorkflow.Any(check => check.Is(workflow, job)));
                if (allReported && !ofWorkflow.Any(check => check.IsPending))
                {
                    return checks;
                }
            }

            await Task.Delay(CiLive.PollInterval, cancellationToken).ConfigureAwait(false);
        }

        Assert.Fail(
            $"Checks of {Url} did not finish within {CiLive.ChecksTimeout}. Last report:{Environment.NewLine}"
            + string.Join(Environment.NewLine, checks));
        return checks;
    }

    /// <summary>mergeStateStatus once GitHub has computed it (CLEAN, BLOCKED, BEHIND, UNSTABLE, DIRTY, …).</summary>
    public async Task<string> MergeStateAsync(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var state = "UNKNOWN";
        while (clock.Elapsed < TimeSpan.FromMinutes(3))
        {
            var view = await GitHubCli
                .JsonAsync(["pr", "view", NumberText, "--json", "mergeStateStatus"], cancellationToken)
                .ConfigureAwait(false);
            state = view.GetProperty("mergeStateStatus").GetString() ?? "UNKNOWN";
            if (!string.Equals(state, "UNKNOWN", StringComparison.Ordinal))
            {
                return state;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        return state;
    }

    /// <summary>
    /// Full log of one job, read through the REST API (<c>actions/jobs/{id}/logs</c>) as soon as that job completes.
    /// <c>gh run view --log-failed</c> only works once the whole run is complete, which is too late when a job fails
    /// fast while the others still run. The log is raw text with ANSI colors, so gh needs
    /// <c>--allow-escape-sequences</c> to print it; it may answer 404 for a few seconds after completion, hence the retry.
    /// </summary>
    public static async Task<string> JobLogAsync(PullRequestCheck check, CancellationToken cancellationToken)
    {
        Assert.True(check.JobId is not null, $"Could not find the job id in the check link: {check}");
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var log = await GitHubCli
                .RunAsync(["api", "--allow-escape-sequences", $"repos/{CiLive.Repository}/actions/jobs/{check.JobId}/logs"], cancellationToken)
                .ConfigureAwait(false);
            if (log.Succeeded && log.StandardOutput.Length > 0)
            {
                return log.StandardOutput;
            }

            Assert.True(clock.Elapsed < JobLogTimeout, $"Could not read the log of job {check.JobId} within {JobLogTimeout}. " + log.Describe());
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Merges with a merge commit (ADR-0011) and returns the merge commit SHA.</summary>
    public async Task<string> MergeAsync(CancellationToken cancellationToken)
    {
        var merge = await GitHubCli.RunAsync(["pr", "merge", NumberText, "--merge", "--delete-branch"], cancellationToken).ConfigureAwait(false);
        Assert.True(merge.Succeeded, "gh pr merge failed for a green PR. " + merge.Describe());

        var view = await GitHubCli
            .JsonAsync(["pr", "view", NumberText, "--json", "state,mergeCommit"], cancellationToken)
            .ConfigureAwait(false);
        Assert.Equal("MERGED", view.GetProperty("state").GetString());
        return view.GetProperty("mergeCommit").GetProperty("oid").GetString() ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        var view = await GitHubCli.RunAsync(["pr", "view", NumberText, "--json", "state", "--jq", ".state"], CancellationToken.None).ConfigureAwait(false);
        if (string.Equals(view.StandardOutput.Trim(), "OPEN", StringComparison.Ordinal))
        {
            await GitHubCli
                .RunAsync(["pr", "close", NumberText, "--delete-branch", "--comment", "Closed by the live CI test suite."], CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Wall time of the workflow: first check start to last check completion (skipped checks ignored).</summary>
    public static TimeSpan Duration(IEnumerable<PullRequestCheck> checks)
    {
        var ran = checks.Where(check => !check.Skipped && check.StartedAt is not null && check.CompletedAt is not null).ToList();
        return ran.Count == 0 ? TimeSpan.Zero : ran.Max(check => check.CompletedAt!.Value) - ran.Min(check => check.StartedAt!.Value);
    }

    internal static string Describe(IEnumerable<PullRequestCheck> checks) =>
        string.Join(Environment.NewLine, checks.Select(check => check.ToString()));
}
