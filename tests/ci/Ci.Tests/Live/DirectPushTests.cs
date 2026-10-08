using System.Text.RegularExpressions;
using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>IT-06: with the protection configured, a direct push to main is rejected by GitHub.</summary>
[Collection(CiLiveDefinition.Name)]
public sealed partial class DirectPushTests
{
    [Fact(
        DisplayName = "Dada a proteção configurada, quando se tenta push direto na main, então o GitHub rejeita",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-06")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenProtectedMainWhenPushingDirectlyThenGitHubRejects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, CiPipeline.RequiredJobs, cancellationToken);

        // Guard: never push to an unprotected main (the probe would land). Fails here, before any change on GitHub.
        await AssertMainRequiresPullRequestsAsync(cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("IT-06", cancellationToken);
        var before = await repository.RemoteHeadAsync(CiLive.BaseBranch, cancellationToken);
        await repository.CommitAsync(allowEmpty: true, cancellationToken);

        var push = await repository.PushAsync("HEAD:refs/heads/" + CiLive.BaseBranch, cancellationToken);
        var after = await repository.RemoteHeadAsync(CiLive.BaseBranch, cancellationToken);

        Assert.False(
            push.Succeeded,
            $"The direct push to {CiLive.BaseBranch} was ACCEPTED (main moved {before} -> {after}); revert that empty commit through a PR. "
            + push.Describe());
        Assert.Matches(ProtectionRejection(), push.Output);
        Assert.Equal(before, after);
    }

    private static async Task AssertMainRequiresPullRequestsAsync(CancellationToken cancellationToken)
    {
        var classic = await GitHubCli.RunAsync(
            ["api", $"repos/{CiLive.Repository}/branches/{CiLive.BaseBranch}/protection", "--jq", ".required_pull_request_reviews != null"],
            cancellationToken).ConfigureAwait(false);
        var rulesets = await GitHubCli.RunAsync(
            ["api", $"repos/{CiLive.Repository}/rules/branches/{CiLive.BaseBranch}", "--jq", "[.[].type] | index(\"pull_request\") != null"],
            cancellationToken).ConfigureAwait(false);

        var protectedByClassic = classic.Succeeded && string.Equals(classic.StandardOutput.Trim(), "true", StringComparison.Ordinal);
        var protectedByRuleset = rulesets.Succeeded && string.Equals(rulesets.StandardOutput.Trim(), "true", StringComparison.Ordinal);
        Assert.True(
            protectedByClassic || protectedByRuleset,
            $"{CiLive.Repository}@{CiLive.BaseBranch} does not require pull requests (branch protection or ruleset); "
            + "refusing to push. " + classic.Describe() + Environment.NewLine + rulesets.Describe());
    }

    [GeneratedRegex("protected branch|GH006|GH013|rule violation|must be made through a pull request", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ProtectionRejection();
}
