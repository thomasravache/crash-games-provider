using System.Security.Cryptography;
using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>IT-03: a PR that commits an AWS-style access key makes the security job fail on gitleaks.</summary>
[Collection(CiLiveDefinition.Name)]
public sealed class SecretScanTests
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    [Fact(
        DisplayName = "Dado um PR com uma chave AWS de exemplo no código, quando o CI roda, então o job security falha no gitleaks",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-03")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenPullRequestWithAwsKeyWhenCiRunsThenSecurityJobFailsOnGitleaks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, [CiPipeline.Security], cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("IT-03", cancellationToken);
        repository.WriteFile("src/backend/Host/CiLiveProbeCredentials.cs", ProbeSource(FakeAccessKeyId()));
        await repository.CommitAsync(allowEmpty: false, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, [CiPipeline.Security], cancellationToken);

        LiveAssert.Bucket(checks, CiPipeline.Security, "fail");
        await LiveAssert.FailedLogContainsAsync(checks, CiPipeline.Security, "gitleaks", cancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine($"IT-03 evidence: {pullRequest.Url}");
    }

    /// <summary>
    /// Fake key in the AWS access key id format, generated at run time so this source file itself holds no secret.
    /// Random (not the documented AKIA…EXAMPLE id, which gitleaks allow-lists).
    /// </summary>
    private static string FakeAccessKeyId() =>
        "AKIA" + new string(RandomNumberGenerator.GetItems(Base32Alphabet.AsSpan(), 16));

    private static string ProbeSource(string accessKeyId) => $$"""
        namespace Apogeu.Host;

        // ci-live probe IT-03: fake credential committed on purpose; the security job must reject it.
        internal static class CiLiveProbeCredentials
        {
            internal const string AwsAccessKeyId = "{{accessKeyId}}";
        }

        """;
}
