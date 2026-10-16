using Apogeu.Deploy.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Deploy.Tests.Integration;

/// <summary>
/// SPEC-0066 IT-01: a compose project of its own on this machine (test ports, Caddy's internal certificates for
/// *.localhost), fed by a fixture repository with three releases: A, B without health and B. vps-deploy publishes A;
/// B without health fails and the script goes back to A by itself; B goes up; vps-rollback goes back from B to A. The
/// version on the air is read through the proxy over HTTPS (api /version and cassino /casino/version), the images are
/// tagged with the commit and no secret of the .env reaches the output of the scripts.
/// </summary>
public sealed class DeployRollbackTests
{
    private static readonly Release[] Releases =
    [
        new("release-a", "A"),
        new("release-b-unhealthy", "B-unhealthy", Healthy: false),
        new("release-b", "B"),
    ];

    [Fact(DisplayName = "Dadas as versões A e B, quando publicadas, então B fica no ar; B sem saúde volta sozinho para A; o vps-rollback volta de B para A")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-01")]
    public async Task GivenVersionsAAndBWhenDeployedThenBGoesUpUnhealthyBGoesBackAndRollbackReturnsToA()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var deployment = await TestDeployment.CreateAsync("it01", Releases, cancellationToken);
        var outputs = new List<ProcessResult>();

        // A
        var deployA = await deployment.DeployAsync("release-a", cancellationToken);
        outputs.Add(deployA);
        Assert.True(deployA.Succeeded, "vps-deploy release-a failed. " + deployA.Describe());
        await AssertOnTheAirAsync(deployment, "A", cancellationToken);
        await AssertImagesTaggedWithAsync(deployment, deployment.CommitOf("release-a"), cancellationToken);
        await AssertHttpRedirectsToHttpsAsync(deployment, cancellationToken);

        // B without health: exit ≠ 0 and A back on the air, by itself.
        var deployUnhealthy = await deployment.DeployAsync("release-b-unhealthy", cancellationToken);
        outputs.Add(deployUnhealthy);
        Assert.False(deployUnhealthy.Succeeded, "vps-deploy of a release without health exited with 0. " + deployUnhealthy.Describe());
        await AssertOnTheAirAsync(deployment, "A", cancellationToken);
        await AssertImagesTaggedWithAsync(deployment, deployment.CommitOf("release-a"), cancellationToken);

        // B
        var deployB = await deployment.DeployAsync("release-b", cancellationToken);
        outputs.Add(deployB);
        Assert.True(deployB.Succeeded, "vps-deploy release-b failed. " + deployB.Describe());
        await AssertOnTheAirAsync(deployment, "B", cancellationToken);
        await AssertImagesTaggedWithAsync(deployment, deployment.CommitOf("release-b"), cancellationToken);

        // Rollback: back to A in one step.
        var rollback = await deployment.RollbackAsync(cancellationToken);
        outputs.Add(rollback);
        Assert.True(rollback.Succeeded, "vps-rollback failed. " + rollback.Describe());
        await AssertOnTheAirAsync(deployment, "A", cancellationToken);
        await AssertImagesTaggedWithAsync(deployment, deployment.CommitOf("release-a"), cancellationToken);

        // No secret in the log of the deploy (SPEC-0066 §5).
        var secrets = deployment.SecretValues();
        Assert.Equal(DeployScripts.Secrets.Length, secrets.Count);
        foreach (var output in outputs)
        {
            Assert.All(secrets, secret => Assert.DoesNotContain(secret, output.Output, StringComparison.Ordinal));
        }

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(deployment.EnvFilePath));
    }

    /// <summary>The api (api.localhost) and the demo-casino behind /casino of the casino site answer the version, over HTTPS.</summary>
    private static async Task AssertOnTheAirAsync(TestDeployment deployment, string version, CancellationToken cancellationToken)
    {
        var api = await deployment.HttpsAsync("api", "/version", cancellationToken).ConfigureAwait(false);
        Assert.True(api.Status == 200 && string.Equals(api.Body.Trim(), version, StringComparison.Ordinal), $"expected version {version} on the api: {api}");
        var casino = await deployment.HttpsAsync("cassino", "/casino/version", cancellationToken).ConfigureAwait(false);
        Assert.True(casino.Status == 200 && string.Equals(casino.Body.Trim(), version, StringComparison.Ordinal), $"expected version {version} on the demo-casino: {casino}");
    }

    /// <summary>The running api and demo-casino come from images tagged with the commit of the release (versions by commit).</summary>
    private static async Task AssertImagesTaggedWithAsync(TestDeployment deployment, string commit, CancellationToken cancellationToken)
    {
        var containers = await DockerCli.ServiceContainersAsync(deployment.Project, cancellationToken).ConfigureAwait(false);
        foreach (var service in new[] { "api", "demo-casino" })
        {
            Assert.True(containers.TryGetValue(service, out var id), $"no running {service} container in {deployment.Project}");
            var image = await DockerCli.RunAsync(["inspect", "--format", "{{.Config.Image}}", id], cancellationToken).ConfigureAwait(false);
            var tag = image.StandardOutput.Trim().Split(':').Last();
            Assert.True(
                tag.Contains(commit[..7], StringComparison.Ordinal),
                $"{service} runs {image.StandardOutput.Trim()}, not an image tagged with the commit {commit}");
        }
    }

    /// <summary>HTTP on the test port answers a redirect to HTTPS (Caddy's automatic redirect).</summary>
    private static async Task AssertHttpRedirectsToHttpsAsync(TestDeployment deployment, CancellationToken cancellationToken)
    {
        foreach (var site in new[] { "cassino", "jogo", "api" })
        {
            var answer = await deployment.HttpAsync(site, "/", cancellationToken).ConfigureAwait(false);
            Assert.True(
                answer.Status is 301 or 302 or 307 or 308 && answer.Location.StartsWith($"https://{site}.{TestDeployment.Domain}", StringComparison.Ordinal),
                $"HTTP of {site} does not redirect to HTTPS: {answer}");
        }
    }
}
