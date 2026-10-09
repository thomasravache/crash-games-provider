using Xunit;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Gate and preconditions of the live tests (Category=CiLive). They open real pull requests on GitHub and spend
/// Actions minutes, so they are skipped unless APOGEU_CI_LIVE=1, and the preconditions fail fast, before any call
/// that changes GitHub, when the pipeline under test is not in place.
/// </summary>
public static class CiLive
{
    public const string Category = "CiLive";
    public const string OptInVariable = "APOGEU_CI_LIVE";
    public const string SkipReason =
        "Live CI test (opens real PRs on GitHub): run with APOGEU_CI_LIVE=1 and GH_TOKEN of the repository owner — see tests/ci/README.md";

    public const string Repository = "thomasravache/crash-games-provider";
    public const string BaseBranch = "main";

    public static readonly TimeSpan ChecksTimeout = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    /// <summary>Read by xUnit through <c>SkipUnless</c>.</summary>
    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal);

    /// <summary>
    /// Local preconditions (no network): the workflow and the jobs the test depends on exist in this checkout, and a
    /// token is available. Then a read-only check that the default branch on GitHub already has the workflow.
    /// </summary>
    public static async Task EnsureReadyAsync(string workflow, IReadOnlyCollection<string> jobs, CancellationToken cancellationToken)
    {
        var file = WorkflowFile.TryLoad(workflow);
        Assert.True(file is not null, $"workflow {workflow} not found in {WorkflowFile.Directory}");

        var declared = file.Jobs().Keys.ToHashSet(StringComparer.Ordinal);
        var missing = jobs.Where(job => !declared.Contains(job)).ToList();
        Assert.True(missing.Count == 0, $"{workflow} does not declare the job(s): {string.Join(", ", missing)}");

        Assert.True(
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GH_TOKEN")),
            "GH_TOKEN is not set: export GH_TOKEN=\"$(gh auth token --user thomasravache)\" before running the live suite.");

        var remote = await GitHubCli
            .RunAsync(["api", $"repos/{Repository}/contents/.github/workflows/{workflow}?ref={BaseBranch}", "--jq", ".path"], cancellationToken)
            .ConfigureAwait(false);
        Assert.True(
            remote.Succeeded,
            $"workflow {workflow} is not on {Repository}@{BaseBranch} yet (the live test runs against the pushed pipeline). " + remote.Describe());
    }
}
