using System.Diagnostics;
using System.Globalization;
using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>
/// IT-05: merging a green PR runs the images workflow on main, which attaches the container images as artifacts.
/// The probe PR carries a single empty commit, so the merge leaves main's content unchanged (only a merge commit).
/// </summary>
[Collection(CiLiveDefinition.Name)]
public sealed class ImagesOnMergeTests
{
    private static readonly TimeSpan RunAppearTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(60);

    [Fact(
        DisplayName = "Dado o merge de um PR verde, quando o workflow images roda, então as imagens são geradas e anexadas como artefato",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-05")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenGreenPullRequestWhenMergedThenImagesWorkflowAttachesImageArtifacts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, CiPipeline.PullRequestJobs, cancellationToken);
        Assert.True(
            WorkflowFile.TryLoad(CiPipeline.ImagesWorkflow) is not null,
            $"workflow {CiPipeline.ImagesWorkflow} not found in {WorkflowFile.Directory}");

        await using var repository = await ProbeRepository.CloneAsync("IT-05", cancellationToken);
        await repository.CommitAsync(allowEmpty: true, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, CiPipeline.PullRequestJobs, cancellationToken);
        var red = checks.Where(check => check.Failed || string.Equals(check.Bucket, "cancel", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(red.Count == 0, "The probe PR is not green:" + Environment.NewLine + ProbePullRequest.Describe(red));
        Assert.Equal("CLEAN", await pullRequest.MergeStateAsync(cancellationToken));

        var mergeCommit = await pullRequest.MergeAsync(cancellationToken);
        var runId = await WaitForImagesRunAsync(mergeCommit, cancellationToken);

        var artifacts = await GitHubCli.RunAsync(
            ["api", $"repos/{CiLive.Repository}/actions/runs/{runId}/artifacts", "--jq", ".artifacts[] | select(.expired | not) | .name"],
            cancellationToken);
        Assert.True(artifacts.Succeeded, artifacts.Describe());
        var names = artifacts.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.True(
            names.Any(name => name.Contains("image", StringComparison.OrdinalIgnoreCase)),
            $"The images run {runId} attached no image artifact (artifacts: {string.Join(", ", names)}).");
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"IT-05 evidence: {pullRequest.Url} merged as {mergeCommit}; images run {runId}; artifacts: {string.Join(", ", names)}");
    }

    private static async Task<string> WaitForImagesRunAsync(string mergeCommit, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        string? runId = null;
        while (runId is null)
        {
            Assert.True(clock.Elapsed < RunAppearTimeout, $"No {CiPipeline.ImagesWorkflow} run for the merge commit {mergeCommit} within {RunAppearTimeout}.");
            var list = await GitHubCli.RunAsync(
                ["run", "list", "--workflow", CiPipeline.ImagesWorkflow, "--commit", mergeCommit, "--json", "databaseId", "--jq", ".[0].databaseId // empty"],
                cancellationToken).ConfigureAwait(false);
            runId = list.Succeeded && list.StandardOutput.Trim().Length > 0 ? list.StandardOutput.Trim() : null;
            if (runId is null)
            {
                await Task.Delay(CiLive.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        clock.Restart();
        while (true)
        {
            var view = await GitHubCli.JsonAsync(["run", "view", runId, "--json", "status,conclusion,url"], cancellationToken).ConfigureAwait(false);
            if (string.Equals(view.GetProperty("status").GetString(), "completed", StringComparison.Ordinal))
            {
                var conclusion = view.GetProperty("conclusion").GetString();
                Assert.True(
                    string.Equals(conclusion, "success", StringComparison.Ordinal),
                    string.Create(CultureInfo.InvariantCulture, $"The images run ended '{conclusion}': {view.GetProperty("url").GetString()}"));
                return runId;
            }

            Assert.True(clock.Elapsed < RunTimeout, $"The images run {runId} did not finish within {RunTimeout}.");
            await Task.Delay(CiLive.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
