using Apogeu.Repository.Tests.Support;

namespace Apogeu.Deploy.Tests.Support;

/// <summary>
/// A test deployment on this machine (SPEC-0066 IT-01, CT-01): a git repository with fake releases (the compose of the
/// contract SPEC-0065@3 with light images, the real infra/caddy, infra/vps and tools/deploy of the working tree), a
/// deployment directory, a compose project of its own and free test ports for the proxy. Disposing removes the
/// containers, volumes, networks and images of the project and the directories.
/// </summary>
internal sealed class TestDeployment : IAsyncDisposable
{
    public const string Domain = "localhost";

    private static readonly string[] CopiedFromWorkingTree = ["infra/caddy", "infra/vps", "tools/deploy"];

    private readonly Dictionary<string, string> _commits = new(StringComparer.Ordinal);

    private TestDeployment(string root, string name)
    {
        Root = root;
        Project = name;
        ImagePrefix = name;
        RepositoryPath = Path.Combine(root, "repository");
        DeployDir = Path.Combine(root, "deploy");
        HttpPort = DockerCli.FreePort();
        HttpsPort = DockerCli.FreePort();
    }

    public string Root { get; }

    /// <summary>Compose project name, unique per test (never the <c>apogeu</c> of the VPS).</summary>
    public string Project { get; }

    public string ImagePrefix { get; }

    public string RepositoryPath { get; }

    public string DeployDir { get; }

    public int HttpPort { get; }

    public int HttpsPort { get; }

    public string EnvFilePath => Path.Combine(DeployDir, ".env");

    /// <summary>The environment of the scripts for this deployment (interface of tests/deploy/README.md).</summary>
    public IReadOnlyDictionary<string, string> Environment => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [DeployScripts.DeployDirVariable] = DeployDir,
        [DeployScripts.RepositoryVariable] = RepositoryPath,
        [DeployScripts.ProjectVariable] = Project,
        [DeployScripts.DomainVariable] = Domain,
        [DeployScripts.HttpPortVariable] = HttpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [DeployScripts.HttpsPortVariable] = HttpsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [DeployScripts.ImagePrefixVariable] = ImagePrefix,
        [DeployScripts.BuildHookVariable] = "deploy-fixture/build",
        [DeployScripts.SmokeVariable] = FixturePath("smoke"),
        [DeployScripts.HealthTimeoutVariable] = "45",
    };

    /// <summary>Creates the fixture repository with one commit and tag per release, in order.</summary>
    public static async Task<TestDeployment> CreateAsync(string purpose, IReadOnlyList<Release> releases, CancellationToken cancellationToken)
    {
        var name = $"apogeu-test-{purpose}-{Guid.NewGuid():N}"[..36].TrimEnd('-');
        var root = Path.Combine(Path.GetTempPath(), name);
        var deployment = new TestDeployment(root, name);
        Directory.CreateDirectory(deployment.RepositoryPath);
        await deployment.InitRepositoryAsync(releases, cancellationToken).ConfigureAwait(false);
        return deployment;
    }

    /// <summary>The commit of a release tag.</summary>
    public string CommitOf(string tag) => _commits[tag];

    public Task<ProcessResult> DeployAsync(string reference, CancellationToken cancellationToken) =>
        DeployScripts.RunAsync(DeployScripts.Deploy, [reference], Environment, cancellationToken);

    public Task<ProcessResult> RollbackAsync(CancellationToken cancellationToken) =>
        DeployScripts.RunAsync(DeployScripts.Rollback, [], Environment, cancellationToken);

    /// <summary>GET over HTTPS through the proxy on the test port (host <c>{site}.localhost</c>).</summary>
    public Task<HttpAnswer> HttpsAsync(string site, string path, CancellationToken cancellationToken) =>
        DockerCli.CurlAsync("https", $"{site}.{Domain}", HttpsPort, path, cancellationToken);

    /// <summary>GET over plain HTTP through the proxy on the test port.</summary>
    public Task<HttpAnswer> HttpAsync(string site, string path, CancellationToken cancellationToken) =>
        DockerCli.CurlAsync("http", $"{site}.{Domain}", HttpPort, path, cancellationToken);

    /// <summary>The secret values of the .env (to assert they never reach the output of a script).</summary>
    public IReadOnlyList<string> SecretValues() =>
        File.Exists(EnvFilePath)
            ? [.. EnvFile.Read(EnvFilePath).Where(pair => DeployScripts.Secrets.Contains(pair.Key) && pair.Value.Length > 0).Select(pair => pair.Value)]
            : [];

    public async ValueTask DisposeAsync()
    {
        await DockerCli.RemoveProjectAsync(Project, ImagePrefix).ConfigureAwait(false);
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a file left by a container under the temporary directory does not fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "release", name);

    private static void CopyDirectory(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(destination, File.GetUnixFileMode(file));
            }
        }
    }

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    /// <summary>The compose of the contract without one service (its block and the depends_on entry on it).</summary>
    private static string ComposeWithout(string compose, string service)
    {
        var kept = new List<string>();
        var skipping = false;
        foreach (var line in compose.Split('\n'))
        {
            var isServiceHeader = line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal) && line.TrimEnd().EndsWith(':');
            if (isServiceHeader || !line.StartsWith(' '))
            {
                skipping = string.Equals(line.TrimEnd(), $"  {service}:", StringComparison.Ordinal);
            }

            if (!skipping)
            {
                kept.Add(line);
            }
        }

        return string.Join('\n', kept);
    }

    private async Task InitRepositoryAsync(IReadOnlyList<Release> releases, CancellationToken cancellationToken)
    {
        await GitAsync(["init", "--quiet", "--initial-branch", "main"], cancellationToken).ConfigureAwait(false);
        foreach (var relative in CopiedFromWorkingTree)
        {
            var source = RepositoryRoot.Combine(relative.Split('/'));
            if (Directory.Exists(source))
            {
                CopyDirectory(source, Path.Combine(RepositoryPath, relative));
            }
        }

        var fixture = Path.Combine(RepositoryPath, "deploy-fixture");
        CopyDirectory(Path.Combine(AppContext.BaseDirectory, "Fixtures", "release"), fixture);
        MakeExecutable(Path.Combine(fixture, "build"));
        MakeExecutable(FixturePath("smoke"));
        var compose = await File.ReadAllTextAsync(Path.Combine(fixture, "docker-compose.yaml"), cancellationToken).ConfigureAwait(false);

        foreach (var release in releases)
        {
            await File.WriteAllTextAsync(Path.Combine(fixture, "version"), release.Version, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(fixture, "healthy"), release.Healthy ? "yes" : "no", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(fixture, "docker-compose.yaml"),
                release.WithoutService is { } service ? ComposeWithout(compose, service) : compose,
                cancellationToken).ConfigureAwait(false);
            await GitAsync(["add", "--all"], cancellationToken).ConfigureAwait(false);
            await GitAsync(
                ["-c", "user.name=SPEC-0066 test", "-c", "user.email=deploy-tests@apogeu.invalid", "commit", "--quiet", "--allow-empty", "-m", "release " + release.Tag],
                cancellationToken).ConfigureAwait(false);
            await GitAsync(["tag", release.Tag], cancellationToken).ConfigureAwait(false);
            var head = await GitAsync(["rev-parse", "HEAD"], cancellationToken).ConfigureAwait(false);
            _commits[release.Tag] = head.StandardOutput.Trim();
        }
    }

    private async Task<ProcessResult> GitAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.GitAsync(RepositoryPath, arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("fixture repository: " + result.Describe());
        }

        return result;
    }
}
