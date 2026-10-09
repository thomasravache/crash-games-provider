using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>
/// IT-02 (amendment v2): a PR with a failing unit test turns the check red and the PR reports a merge state other than
/// CLEAN. Blocking the merge on the server is SPEC-0015's (no branch protection on a private GitHub Free repository).
/// </summary>
[Collection(CiLiveDefinition.Name)]
public sealed class BrokenTestTests
{
    private const string ProbeTestName = "CiLiveProbeFailsOnPurpose";
    private static readonly TimeSpan FullPullRequestBudget = TimeSpan.FromMinutes(20);

    private static readonly string FailingTest = $$"""
        using Xunit;

        namespace Apogeu.ServiceDefaults.Tests.Unit;

        public sealed class CiLiveProbeTests
        {
            [Fact]
            public void {{ProbeTestName}}() => Assert.Fail("ci-live probe IT-02: this test fails on purpose.");
        }

        """;

    [Fact(
        DisplayName = "Dado um PR com um teste unitário falhando, quando o CI roda, então o check fica vermelho e o PR reporta mergeStateStatus diferente de CLEAN",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-02")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenPullRequestWithFailingUnitTestWhenCiRunsThenCheckIsRedAndMergeStateIsNotClean()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, CiPipeline.PullRequestJobs, cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("IT-02", cancellationToken);
        repository.WriteFile("tests/backend/ServiceDefaults.Tests/Unit/CiLiveProbeTests.cs", FailingTest);
        await repository.CommitAsync(allowEmpty: false, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, CiPipeline.PullRequestJobs, cancellationToken);

        LiveAssert.Bucket(checks, CiPipeline.Backend, "fail");
        await LiveAssert.FailedLogContainsAsync(checks, CiPipeline.Backend, ProbeTestName, cancellationToken);

        var mergeState = await pullRequest.MergeStateAsync(cancellationToken);
        Assert.False(
            string.Equals(mergeState, "CLEAN", StringComparison.Ordinal),
            $"Expected mergeStateStatus other than CLEAN with a red check, but it is {mergeState}: {pullRequest.Url}");

        // NFR (section 5): a PR that runs the whole pipeline within 20 minutes.
        var duration = ProbePullRequest.Duration(checks);
        Assert.True(duration <= FullPullRequestBudget, $"PR took {duration} (budget {FullPullRequestBudget}): {pullRequest.Url}");
        TestContext.Current.TestOutputHelper?.WriteLine($"IT-02 evidence: {pullRequest.Url} — CI wall time {duration}");
    }
}
