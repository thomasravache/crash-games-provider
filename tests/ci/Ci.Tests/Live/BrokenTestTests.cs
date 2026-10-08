using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>IT-02: a PR with a failing unit test turns the check red and merging is blocked.</summary>
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
        DisplayName = "Dado um PR com um teste unitário falhando, quando o CI roda, então o check fica vermelho e o merge fica bloqueado",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-02")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenPullRequestWithFailingUnitTestWhenCiRunsThenCheckIsRedAndMergeIsBlocked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, CiPipeline.RequiredJobs, cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("IT-02", cancellationToken);
        repository.WriteFile("tests/backend/ServiceDefaults.Tests/Unit/CiLiveProbeTests.cs", FailingTest);
        await repository.CommitAsync(allowEmpty: false, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, CiPipeline.RequiredJobs, cancellationToken);

        LiveAssert.Bucket(checks, CiPipeline.Backend, "fail");
        await LiveAssert.FailedLogContainsAsync(checks, CiPipeline.Backend, ProbeTestName, cancellationToken);

        var mergeState = await pullRequest.MergeStateAsync(cancellationToken);
        Assert.True(
            string.Equals(mergeState, "BLOCKED", StringComparison.Ordinal),
            $"Expected the merge to be BLOCKED by the required checks, but mergeStateStatus is {mergeState}: {pullRequest.Url}");

        // NFR (section 5): a PR that runs the whole pipeline within 20 minutes.
        var duration = ProbePullRequest.Duration(checks);
        Assert.True(duration <= FullPullRequestBudget, $"PR took {duration} (budget {FullPullRequestBudget}): {pullRequest.Url}");
        TestContext.Current.TestOutputHelper?.WriteLine($"IT-02 evidence: {pullRequest.Url} — CI wall time {duration}");
    }
}
