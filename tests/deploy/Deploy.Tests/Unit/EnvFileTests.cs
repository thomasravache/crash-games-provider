using Apogeu.Deploy.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Deploy.Tests.Unit;

/// <summary>
/// SPEC-0066 UT-02: <c>tools/deploy/vps-deploy --prepare</c> (prepares the deployment directory and stops: no fetch, no
/// build, no compose) creates the .env of the contract SPEC-0065@2 the first time, with every secret random, permission
/// 600 and nothing of it printed; a second run keeps the same values.
/// </summary>
public sealed class EnvFileTests : IDisposable
{
    private const string Domain = "143-95-218-240.sslip.io";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "apogeu-test-ut02-" + Guid.NewGuid().ToString("N"));

    [Fact(DisplayName = "Dado um diretório de deploy sem .env, quando o vps-deploy prepara a versão, então cria o .env com segredos aleatórios, 600 e sem imprimi-los")]
    [Trait("Category", "Unit")]
    [Trait("Spec", "SPEC-0066:UT-02")]
    public async Task GivenADeployDirWithoutEnvWhenPreparedThenItCreatesTheEnvWithRandomSecrets()
    {
        var deployDir = Path.Combine(_root, "deploy");

        var result = await PrepareAsync(deployDir);

        Assert.True(result.Succeeded, "vps-deploy --prepare failed. " + result.Describe());
        var envFile = Path.Combine(deployDir, ".env");
        Assert.True(File.Exists(envFile), "vps-deploy --prepare did not create the .env. " + result.Describe());
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(envFile));

        var values = EnvFile.Read(envFile);
        foreach (var secret in DeployScripts.Secrets)
        {
            Assert.True(values.TryGetValue(secret, out var value) && value.Length > 0, $".env without a value for {secret}");
            var minimum = string.Equals(secret, "OPERATORS_DEMO_KEY_ID", StringComparison.Ordinal) ? 8 : 32;
            Assert.True(value.Length >= minimum, $"{secret} has {value.Length} characters; expected at least {minimum} of random text");
            Assert.True(IsSafeEnvValue(value), $"{secret} has characters that break the .env or the compose interpolation");
            Assert.DoesNotContain(value, result.Output, StringComparison.Ordinal);
        }

        Assert.Equal(DeployScripts.Secrets.Length, DeployScripts.Secrets.Select(secret => values[secret]).Distinct(StringComparer.Ordinal).Count());
        foreach (var setting in DeployScripts.Settings)
        {
            Assert.True(values.ContainsKey(setting), $".env without {setting} (SPEC-0065@2 §6)");
        }

        Assert.Equal($"https://api.{Domain}", values["PUBLIC_API_URL"]);
        Assert.Equal($"https://jogo.{Domain}", values["PUBLIC_GAME_URL"]);

        // Random, not fixed: another deployment directory gets other secrets.
        var other = await PrepareAsync(Path.Combine(_root, "other"));
        Assert.True(other.Succeeded, other.Describe());
        var otherValues = EnvFile.Read(Path.Combine(_root, "other", ".env"));
        Assert.All(DeployScripts.Secrets, secret => Assert.False(
            string.Equals(values[secret], otherValues[secret], StringComparison.Ordinal), $"{secret} is the same in two deployment directories"));
    }

    [Fact(DisplayName = "Dado um .env já criado, quando o vps-deploy prepara de novo, então mantém os mesmos valores e a permissão 600")]
    [Trait("Category", "Unit")]
    [Trait("Spec", "SPEC-0066:UT-02")]
    public async Task GivenAnExistingEnvWhenPreparedAgainThenItKeepsTheValues()
    {
        var deployDir = Path.Combine(_root, "deploy");
        var first = await PrepareAsync(deployDir);
        Assert.True(first.Succeeded, "vps-deploy --prepare failed. " + first.Describe());
        var envFile = Path.Combine(deployDir, ".env");
        var before = EnvFile.Read(envFile);

        var second = await PrepareAsync(deployDir);

        Assert.True(second.Succeeded, "second vps-deploy --prepare failed. " + second.Describe());
        var after = EnvFile.Read(envFile);
        Assert.All(DeployScripts.Secrets, secret => Assert.Equal(before[secret], after[secret]));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(envFile));
        Assert.All(DeployScripts.Secrets, secret => Assert.DoesNotContain(after[secret], second.Output, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>No whitespace, quotes, <c>$</c> (compose interpolation), <c>#</c> (comment) nor backslash.</summary>
    private static bool IsSafeEnvValue(string value) =>
        value.All(character => !char.IsWhiteSpace(character) && character is not ('"' or '\'' or '$' or '#' or '\\' or '`'));

    private static Task<ProcessResult> PrepareAsync(string deployDir) =>
        DeployScripts.RunAsync(
            DeployScripts.Deploy,
            ["--prepare"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DeployScripts.DeployDirVariable] = deployDir,
                [DeployScripts.DomainVariable] = Domain,
                [DeployScripts.ProjectVariable] = "apogeu-test-ut02",
            },
            TestContext.Current.CancellationToken);
}
