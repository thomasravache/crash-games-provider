using System.Reflection;
using System.Security.Cryptography;
using Xunit;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// SPEC-0065: runs the AppHost once in publish mode, as <c>aspire publish</c> does (<c>--operation publish --step publish
/// --output-path</c>), into a temporary directory, and keeps the outcome for the tests of the collection. The built AppHost
/// runs with <c>dotnet run --no-build --no-launch-profile</c> in <c>Production</c>, so no launch profile of the local run
/// (Development) applies. Every secret parameter of the §6 contract (and the generated ones of the AppHost) gets a random
/// canary value through the configuration: none may reach the generated files. The publish never throws here; each test
/// asserts <see cref="AssertPublished"/> first, so a failing publish is reported with its output.
/// </summary>
public sealed class PublishedComposeFixture : IAsyncLifetime
{
    public const string ComposeFileName = "docker-compose.yaml";

    /// <summary>Address of the game given as the <c>public-game-url</c> parameter.</summary>
    public const string PublicGameUrl = "https://jogo.canary.apogeu.test";

    /// <summary>Address of the API given as the <c>public-api-url</c> parameter.</summary>
    public const string PublicApiUrl = "https://api.canary.apogeu.test";

    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Parameters given secret canary values: the §6 secrets by the parameter name of their variable (lower case, dashes),
    /// plus the generated passwords Aspire would add by default.
    /// </summary>
    private static readonly string[] SecretParameters =
    [
        "operators-demo-key-secret",
        "player-session-signing-key",
        "postgres-password",
        "keycloak-admin-password",
        "keycloak-password",
        "redis-password",
    ];

    private ProcessResult? _process;

    /// <summary>The directory the publish wrote to.</summary>
    public string OutputDirectory { get; } = Directory.CreateTempSubdirectory("apogeu-compose-").FullName;

    /// <summary>Secret values handed to the AppHost (parameter name → value): none may appear in the output.</summary>
    public IReadOnlyDictionary<string, string> Canaries { get; } = SecretParameters.ToDictionary(
        name => name,
        _ => "canary" + RandomNumberGenerator.GetHexString(32, lowercase: true),
        StringComparer.Ordinal);

    public string ComposePath => Path.Combine(OutputDirectory, ComposeFileName);

    public async ValueTask InitializeAsync()
    {
        var appHostProject = Projects.AppHost.ProjectPath;
        var configuration = typeof(PublishedComposeFixture).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

        string[] arguments =
        [
            "run",
            "--project", appHostProject,
            "--no-build",
            "--no-launch-profile",
            "-c", configuration,
            "--",
            "--environment", "Production",
            "--operation", "publish",
            "--step", "publish",
            "--output-path", OutputDirectory,
            $"--Parameters:public-game-url={PublicGameUrl}",
            $"--Parameters:public-api-url={PublicApiUrl}",
            .. Canaries.Select(canary => $"--Parameters:{canary.Key}={canary.Value}"),
        ];

        _process = await ProcessRunner.RunAsync("dotnet", arguments, RepositoryRoot.Path, PublishTimeout, CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>Fails unless the publish exited with 0 and wrote the compose file.</summary>
    public void AssertPublished()
    {
        Assert.True(_process is not null, "Setup failed: the AppHost publish did not run.");
        Assert.True(
            _process.ExitCode == 0,
            "The AppHost publish (aspire publish) failed. " + Describe());
        Assert.True(
            File.Exists(ComposePath),
            $"The AppHost publish wrote no {ComposeFileName} to {OutputDirectory}. " + Describe());
    }

    /// <summary>
    /// The example environment file written next to the compose file: <c>.env.example</c> (§6) or, failing that, the
    /// <c>.env</c> Aspire writes. Fails when there is none.
    /// </summary>
    public string EnvTemplatePath()
    {
        string[] candidates = [Path.Combine(OutputDirectory, ".env.example"), Path.Combine(OutputDirectory, ".env")];
        var found = candidates.FirstOrDefault(File.Exists);
        Assert.True(
            found is not null,
            $"The AppHost publish wrote neither .env.example nor .env to {OutputDirectory}. Files: {string.Join(", ", Files())}.");
        return found;
    }

    /// <summary>Every file the publish wrote, relative to the output directory.</summary>
    public IReadOnlyList<string> Files() =>
        Directory.EnumerateFiles(OutputDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(OutputDirectory, path))
            .Order(StringComparer.Ordinal)
            .ToList();

    public string Describe() => _process?.Describe() ?? "(not run)";

    public ValueTask DisposeAsync()
    {
        Directory.Delete(OutputDirectory, recursive: true);
        return ValueTask.CompletedTask;
    }
}
