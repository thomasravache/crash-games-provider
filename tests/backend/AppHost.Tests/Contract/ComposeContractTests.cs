using System.Text.Json;
using System.Text.RegularExpressions;
using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Contract;

/// <summary>
/// SPEC-0065 CT-01: the contract SPEC-0066 consumes (§6) — the published compose and its .env without values, as
/// Docker Compose itself resolves them (<c>docker compose config</c>, client side only: nothing is pulled or started).
/// </summary>
[Collection(PublishedComposeGroup.Name)]
public sealed partial class ComposeContractTests(PublishedComposeFixture published)
{
    private static readonly TimeSpan DockerTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Services of §6 (Emenda v3: the short-lived keys-init included).</summary>
    private static readonly string[] ContractServices = ["api", "demo-casino", "keys-init", "postgres", "redis"];

    /// <summary>The short-lived service that hands the key volume to the api user (Emenda v3): it needs no network.</summary>
    private const string KeysInit = "keys-init";

    /// <summary>Volumes of §6.</summary>
    private static readonly string[] ContractVolumes = ["dataprotection-keys", "postgres-data"];

    /// <summary>Variables of the .env (§6, Emenda v2), the images of the version included.</summary>
    private static readonly string[] ContractVariables =
    [
        "API_IMAGE",
        "BACKOFFICE_AUTHORITY",
        "DEMO_CASINO_IMAGE",
        "OPERATORS_DEMO_KEY_ID",
        "OPERATORS_DEMO_KEY_SECRET",
        "PLAYER_SESSION_SIGNING_KEY",
        "POSTGRES_PASSWORD",
        "PUBLIC_API_URL",
        "PUBLIC_GAME_URL",
        "REDIS_PASSWORD",
    ];

    /// <summary>Services the proxy of SPEC-0066 reaches by name on port 8080 (§6).</summary>
    private static readonly string[] ProxiedServices = ["api", "demo-casino"];

    [Fact(DisplayName = "Dado o compose gerado e o .env, quando validados pelo docker compose config, então o arquivo é válido e declara exatamente os serviços, a rede, os volumes e as variáveis do contrato que a SPEC-0066 consome")]
    [Trait("Spec", "SPEC-0065:CT-01")]
    [Trait("Category", "Contract")]
    [Trait("Category", "Integration")]
    public async Task GivenPublishedComposeAndEnvExampleWhenValidatedByDockerComposeConfigThenTheyDeclareExactlyTheContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        published.AssertPublished();

        // The variables of the .env: exactly those of §6.
        var declared = EnvFile.Read(published.EnvTemplatePath()).Keys.ToHashSet(StringComparer.Ordinal);
        var missing = ContractVariables.Where(variable => !declared.Contains(variable)).ToList();
        Assert.True(missing.Count == 0, $"The .env lacks the §6 variables {string.Join(", ", missing)}. Declared: {Sorted(declared)}.");
        var unexpected = declared.Where(variable => !ContractVariables.Contains(variable, StringComparer.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, $"The .env declares variables outside the §6 contract: {Sorted(unexpected)}.");

        // Every variable the compose references is declared in the .env.
        var composeText = await File.ReadAllTextAsync(published.ComposePath, cancellationToken);
        var undeclared = VariableReference().Matches(composeText)
            .Select(match => match.Groups["name"].Value)
            .Where(name => !declared.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(undeclared.Count == 0, $"The compose references variables the .env does not declare: {Sorted(undeclared)}.");

        var resolved = await ResolveAsync(declared, cancellationToken);
        var root = resolved.RootElement;

        Assert.Equal(ContractServices, Names(root, "services"));
        Assert.Equal(ContractVolumes, Names(root, "volumes"));
        var network = Assert.Single(Names(root, "networks"));

        foreach (var service in root.GetProperty("services").EnumerateObject())
        {
            Assert.True(
                string.Equals(service.Name, KeysInit, StringComparison.Ordinal)
                    || (service.Value.TryGetProperty("networks", out var networks) && networks.TryGetProperty(network, out _)),
                $"The service '{service.Name}' is not on the network '{network}' shared with the proxy of SPEC-0066.");
            Assert.False(
                service.Value.TryGetProperty("ports", out var ports) && ports.GetArrayLength() > 0,
                $"The service '{service.Name}' publishes ports on the host: {(service.Value.TryGetProperty("ports", out var shown) ? shown.GetRawText() : string.Empty)}.");
        }

        foreach (var service in ProxiedServices)
        {
            var definition = root.GetProperty("services").GetProperty(service);
            var expose = definition.TryGetProperty("expose", out var exposed)
                ? exposed.EnumerateArray().Select(port => port.ToString()).ToList()
                : [];
            Assert.True(expose.Contains("8080"), $"The service '{service}' must be reachable by name on port 8080 (§6); it exposes {string.Join(", ", expose)}.");
        }
    }

    /// <summary>
    /// Runs <c>docker compose config</c> on the published compose with an environment file filling every declared variable
    /// with a test value, and returns the resolved model. Fails on an invalid file or on a variable left unset.
    /// </summary>
    private async Task<JsonDocument> ResolveAsync(IReadOnlyCollection<string> variables, CancellationToken cancellationToken)
    {
        var scratch = Directory.CreateTempSubdirectory("apogeu-ct01-").FullName;
        try
        {
            var envFile = Path.Combine(scratch, "test.env");
            await ComposeTestEnvironment.WriteAsync(envFile, variables, cancellationToken).ConfigureAwait(false);
            var output = Path.Combine(scratch, "resolved.json");

            var config = await ProcessRunner.RunAsync(
                "docker",
                ["compose", "--project-name", "apogeu-ct01", "--project-directory", published.OutputDirectory, "--env-file", envFile,
                    "-f", published.ComposePath, "config", "--format", "json", "--output", output],
                published.OutputDirectory,
                DockerTimeout,
                cancellationToken).ConfigureAwait(false);
            Assert.True(config.ExitCode == 0, "docker compose config rejected the published compose. " + config.Describe());
            Assert.False(
                config.Output.Contains("is not set", StringComparison.OrdinalIgnoreCase),
                "docker compose config found variables the contract does not declare. " + config.Describe());

            return JsonDocument.Parse(await File.ReadAllTextAsync(output, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static string[] Names(JsonElement root, string section) =>
        root.TryGetProperty(section, out var element) && element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray()
            : [];

    private static string Sorted(IEnumerable<string> names) => string.Join(", ", names.Order(StringComparer.Ordinal));

    /// <summary>A <c>${NAME}</c> reference (with or without a default or required marker); <c>$${…}</c> is an escape.</summary>
    [GeneratedRegex(@"(?<!\$)\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:[:?\-+][^}]*)?\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VariableReference();
}
