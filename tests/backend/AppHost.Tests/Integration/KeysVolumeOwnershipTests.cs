using Apogeu.AppHost.Tests.Support;
using Apogeu.AppHost.Tests.Unit;
using Xunit;

namespace Apogeu.AppHost.Tests.Integration;

/// <summary>
/// SPEC-0065 IT-03 (a) (Emenda v3): with a real Docker volume, the user of the api (UID 1654) cannot write to a new volume
/// (Docker creates it owned by root), and can once the <c>keys-init</c> service of the published compose ran on it — the
/// same image and command, run by <c>docker compose run</c> on the published file.
/// </summary>
[Collection(PublishedComposeGroup.Name)]
public sealed class KeysVolumeOwnershipTests(PublishedComposeFixture published)
{
    private const string KeysVolume = "dataprotection-keys";
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromMinutes(3);

    [Fact(DisplayName = "Dado um volume Docker novo montado num contêiner que roda como o usuário 1654, quando o keys-init roda antes, então o usuário grava em /keys (e sem o keys-init não grava)")]
    [Trait("Spec", "SPEC-0065:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenNewDockerVolumeWhenKeysInitRunsFirstThenTheApiUserWritesToKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        published.AssertPublished();
        var compose = ComposeDocument.Load(published.ComposePath);
        Assert.True(
            compose.HasService(KeysVolumeService),
            $"The published compose has no '{KeysVolumeService}' service: nothing hands the volume {KeysVolume} to the api user. Services: {string.Join(", ", compose.ServiceNames)}.");

        var project = "apogeu-it03-" + Guid.NewGuid().ToString("N")[..12];
        var scratch = Directory.CreateTempSubdirectory("apogeu-it03-").FullName;
        var controlVolume = project + "-control";
        try
        {
            // Control: a volume Docker just created is not writable by the api user.
            var created = await DockerAsync(["volume", "create", controlVolume], ct);
            Assert.True(created.ExitCode == 0, "Setup failed: docker volume create. " + created.Describe());
            var denied = await WriteAsApiUserAsync(controlVolume, ct);
            Assert.True(denied.ExitCode != 0, "Setup failed: the api user wrote to a new volume without keys-init, so the control proves nothing. " + denied.Describe());

            // keys-init of the published compose on the project's own new key volume, then the api user writes there.
            var envFile = Path.Combine(scratch, "test.env");
            await ComposeTestEnvironment.WriteAsync(envFile, EnvFile.Read(published.EnvTemplatePath()).Keys, ct);
            var keysInit = await DockerAsync(
                ["compose", "--project-name", project, "--project-directory", published.OutputDirectory, "--env-file", envFile,
                    "-f", published.ComposePath, "run", "--rm", "--no-deps", KeysVolumeService],
                ct);
            Assert.True(keysInit.ExitCode == 0, $"{KeysVolumeService} of the published compose failed. " + keysInit.Describe());

            var written = await WriteAsApiUserAsync($"{project}_{KeysVolume}", ct);
            Assert.True(written.ExitCode == 0, $"After {KeysVolumeService} the api user (UID 1654) still cannot write to /keys. " + written.Describe());
        }
        finally
        {
            await DockerAsync(["compose", "--project-name", project, "-f", published.ComposePath, "down", "--volumes"], CancellationToken.None);
            await DockerAsync(["volume", "rm", "--force", controlVolume, $"{project}_{KeysVolume}"], CancellationToken.None);
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static string KeysVolumeService => KeysInitServiceTests.KeysInit;

    /// <summary>As the api user (UID 1654), creates a file in the volume mounted at /keys (the reviewer's reproduction).</summary>
    private static Task<ProcessResult> WriteAsApiUserAsync(string volume, CancellationToken ct) =>
        DockerAsync(["run", "--rm", "-u", "1654", "-v", $"{volume}:/keys", KeysInitServiceTests.KeysInitImage, "touch", "/keys/x"], ct);

    private static Task<ProcessResult> DockerAsync(IEnumerable<string> arguments, CancellationToken ct) =>
        ProcessRunner.RunAsync("docker", arguments, RepositoryRoot.Path, DockerTimeout, ct);
}
