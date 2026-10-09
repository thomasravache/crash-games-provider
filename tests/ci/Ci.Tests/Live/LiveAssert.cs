using System.Text.RegularExpressions;
using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

internal static partial class LiveAssert
{
    private const int LogExcerpt = 6000;

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
        var output = await FailedToolOutputAsync(checks, job, cancellationToken).ConfigureAwait(false);
        Assert.True(
            output.Contains(expected, StringComparison.OrdinalIgnoreCase),
            $"The tool output of {CiPipeline.CiWorkflowName}/{job} does not contain '{expected}' (failed for another reason?):"
            + Environment.NewLine + Excerpt(output));
    }

    /// <summary>
    /// Asserts that the output printed by the tools of the job (not the step names) matches every pattern, so a step that
    /// fails for another reason (missing tool, broken build, failed download) does not satisfy the test.
    /// </summary>
    public static async Task FailedOutputMatchesAsync(
        IReadOnlyList<PullRequestCheck> checks,
        string job,
        CancellationToken cancellationToken,
        params Regex[] patterns)
    {
        var output = await FailedToolOutputAsync(checks, job, cancellationToken).ConfigureAwait(false);
        var missing = patterns.Where(pattern => !pattern.IsMatch(output)).Select(pattern => pattern.ToString()).ToList();
        Assert.True(
            missing.Count == 0,
            $"The tool output of {CiPipeline.CiWorkflowName}/{job} does not match {string.Join(", ", missing)} "
            + "(failed for another reason?):" + Environment.NewLine + Excerpt(output));
    }

    /// <summary>
    /// Text the tools printed in the job (log read with <see cref="ProbePullRequest.JobLogAsync"/>). Every API log line is
    /// <c>timestamp message</c>; timestamps and ANSI colors are removed, and the runner's own lines are dropped: the
    /// <c>##[group]Run …</c> … <c>##[endgroup]</c> blocks that echo each step's name and script, other <c>##[…]</c>
    /// annotations and <c>[command]…</c> echoes. So a step name or script never satisfies an assertion.
    /// </summary>
    public static async Task<string> FailedToolOutputAsync(
        IReadOnlyList<PullRequestCheck> checks,
        string job,
        CancellationToken cancellationToken)
    {
        var log = await ProbePullRequest.JobLogAsync(Job(checks, job), cancellationToken).ConfigureAwait(false);
        return ToolOutput(log);
    }

    internal static string ToolOutput(string log)
    {
        var output = new List<string>();
        var inGroup = false;
        foreach (var raw in log.Split('\n'))
        {
            var line = Ansi().Replace(Timestamp().Replace(raw.TrimEnd('\r'), string.Empty, 1), string.Empty);
            if (line.StartsWith("##[group]", StringComparison.Ordinal))
            {
                inGroup = true;
                continue;
            }

            if (line.StartsWith("##[endgroup]", StringComparison.Ordinal))
            {
                inGroup = false;
                continue;
            }

            if (inGroup || line.StartsWith("##[", StringComparison.Ordinal) || line.StartsWith("[command]", StringComparison.Ordinal))
            {
                continue;
            }

            output.Add(line);
        }

        return string.Join('\n', output);
    }

    private static string Excerpt(string output) => output.Length > LogExcerpt ? "[...]" + output[^LogExcerpt..] : output;

    [GeneratedRegex(@"^\uFEFF?\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z ?", RegexOptions.None, 1000)]
    private static partial Regex Timestamp();

    [GeneratedRegex(@"(?:\x1B|\^\[)\[[0-9;]*[A-Za-z]", RegexOptions.None, 1000)]
    private static partial Regex Ansi();
}
