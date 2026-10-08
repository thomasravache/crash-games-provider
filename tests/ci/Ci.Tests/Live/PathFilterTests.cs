using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>IT-01: a front-end only PR runs frontend, e2e, contracts and sdd, and the backend job reports skipped.</summary>
[Collection(CiLiveDefinition.Name)]
public sealed class PathFilterTests
{
    private static readonly TimeSpan FrontOnlyBudget = TimeSpan.FromMinutes(8);

    [Fact(
        DisplayName = "Dado um PR que altera só src/frontend/**, quando o CI roda, então frontend, e2e, contracts e sdd executam e o backend reporta skipped",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-01")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenFrontendOnlyPullRequestWhenCiRunsThenBackendIsSkippedAndFrontendJobsRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, CiPipeline.RequiredJobs, cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("IT-01", cancellationToken);
        repository.AppendToFile("src/frontend/packages/game-sdk/src/index.ts", "// ci-live probe IT-01: front-end only change\n");
        await repository.CommitAsync(allowEmpty: false, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, CiPipeline.RequiredJobs, cancellationToken);

        LiveAssert.Bucket(checks, CiPipeline.Backend, "skipping");
        LiveAssert.Bucket(checks, CiPipeline.Frontend, "pass");
        LiveAssert.Bucket(checks, CiPipeline.E2e, "pass");
        LiveAssert.Bucket(checks, CiPipeline.Contracts, "pass");
        LiveAssert.Bucket(checks, CiPipeline.Sdd, "pass");

        // NFR (section 5): front-end only PR within 8 minutes. Evidence: the PR link printed on failure.
        var duration = ProbePullRequest.Duration(checks);
        Assert.True(duration <= FrontOnlyBudget, $"Front-end only PR took {duration} (budget {FrontOnlyBudget}): {pullRequest.Url}");
        TestContext.Current.TestOutputHelper?.WriteLine($"IT-01 evidence: {pullRequest.Url} — CI wall time {duration}");
    }
}
