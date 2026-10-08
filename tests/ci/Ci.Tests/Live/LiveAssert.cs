using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

internal static class LiveAssert
{
    /// <summary>The check of job <paramref name="job"/> of the ci workflow; fails when it was not reported.</summary>
    public static PullRequestCheck Job(IReadOnlyList<PullRequestCheck> checks, string job)
    {
        var check = checks.FirstOrDefault(item => item.Is(CiPipeline.CiWorkflowName, job));
        Assert.True(
            check is not null,
            $"Check {CiPipeline.CiWorkflowName}/{job} was not reported. Checks:{Environment.NewLine}{ProbePullRequest.Describe(checks)}");
        return check;
    }

    public static void Bucket(IReadOnlyList<PullRequestCheck> checks, string job, string expectedBucket)
    {
        var check = Job(checks, job);
        Assert.True(
            string.Equals(check.Bucket, expectedBucket, StringComparison.OrdinalIgnoreCase),
            $"Expected {CiPipeline.CiWorkflowName}/{job} to be '{expectedBucket}' but it is '{check.Bucket}'. Checks:{Environment.NewLine}"
            + ProbePullRequest.Describe(checks));
    }

    public static async Task FailedLogContainsAsync(
        IReadOnlyList<PullRequestCheck> checks,
        string job,
        string expected,
        CancellationToken cancellationToken)
    {
        var log = await ProbePullRequest.FailedLogAsync(Job(checks, job), cancellationToken).ConfigureAwait(false);
        Assert.True(
            log.Contains(expected, StringComparison.OrdinalIgnoreCase),
            $"The failed steps of {CiPipeline.CiWorkflowName}/{job} do not mention '{expected}' (failed for another reason?):"
            + Environment.NewLine + (log.Length > 6000 ? "[...]" + log[^6000..] : log));
    }
}
