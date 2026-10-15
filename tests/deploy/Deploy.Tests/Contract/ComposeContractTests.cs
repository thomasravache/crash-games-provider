using Apogeu.Deploy.Tests.Support;
using Xunit;

namespace Apogeu.Deploy.Tests.Contract;

/// <summary>
/// SPEC-0066 CT-01, consumer side of SPEC-0065@2: the compose of the contract (fixture with light images in the roles of
/// api and demo-casino; services api, demo-casino, postgres and redis, volumes postgres-data and dataprotection-keys, a
/// single network, api and demo-casino on 8080, the variables of the .env) is read by vps-deploy: the services, the
/// network and the variables exist in the running project, and the proxy reaches api and demo-casino by name. A compose
/// without a service of the contract is refused before anything goes up.
/// </summary>
public sealed class ComposeContractTests
{
    private static readonly string[] ContractServices = ["api", "demo-casino", "postgres", "redis"];

    [Fact(DisplayName = "Dado o compose da SPEC-0065@2, quando o vps-deploy o lê, então serviços, rede e variáveis existem e o proxy alcança api e demo-casino pelo nome")]
    [Trait("Category", "Contract")]
    [Trait("Spec", "SPEC-0066:CT-01")]
    public async Task GivenTheContractComposeWhenDeployedThenServicesNetworkAndVariablesExistAndTheProxyReachesThemByName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var deployment = await TestDeployment.CreateAsync("ct01", [new Release("release-a", "A")], cancellationToken);

        var deploy = await deployment.DeployAsync("release-a", cancellationToken);

        Assert.True(deploy.Succeeded, "vps-deploy of the contract compose failed. " + deploy.Describe());
        var containers = await DockerCli.ServiceContainersAsync(deployment.Project, cancellationToken);
        foreach (var service in ContractServices.Append("proxy"))
        {
            Assert.True(containers.ContainsKey(service), $"service {service} not running in {deployment.Project}: {string.Join(", ", containers.Keys)}");
        }

        // The single network of the contract, shared by the proxy, the api and the demo-casino.
        var networks = (await DockerCli.RunAsync(
                ["network", "ls", "--filter", "label=com.docker.compose.project=" + deployment.Project, "--format", "{{.Name}}"],
                cancellationToken)).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains($"{deployment.Project}_aspire", networks, StringComparer.Ordinal);
        foreach (var service in new[] { "proxy", "api", "demo-casino", "postgres", "redis" })
        {
            var attached = await DockerCli.RunAsync(
                ["inspect", "--format", "{{range $name, $_ := .NetworkSettings.Networks}}{{$name}} {{end}}", containers[service]],
                cancellationToken);
            Assert.Contains($"{deployment.Project}_aspire", attached.StandardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        // Only the proxy publishes ports on the host (ADR-0029), and only the test ports.
        foreach (var service in ContractServices)
        {
            var ports = await DockerCli.RunAsync(["port", containers[service]], cancellationToken);
            Assert.True(string.IsNullOrWhiteSpace(ports.StandardOutput), $"{service} publishes ports on the host: {ports.StandardOutput}");
        }

        var proxyPorts = await DockerCli.RunAsync(["port", containers["proxy"]], cancellationToken);
        Assert.Contains($":{deployment.HttpsPort}", proxyPorts.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($":{deployment.HttpPort}", proxyPorts.StandardOutput, StringComparison.Ordinal);

        // The proxy reaches api:8080 and demo-casino:8080 by name.
        var api = await DockerCli.RunAsync(["exec", containers["proxy"], "wget", "-q", "-O", "-", "http://api:8080/version"], cancellationToken);
        Assert.True(api.Succeeded && string.Equals(api.StandardOutput.Trim(), "A", StringComparison.Ordinal), "the proxy does not reach api:8080 by name. " + api.Describe());
        var casino = await DockerCli.RunAsync(["exec", containers["proxy"], "wget", "-q", "-O", "-", "http://demo-casino:8080/casino/version"], cancellationToken);
        Assert.True(casino.Succeeded && string.Equals(casino.StandardOutput.Trim(), "A", StringComparison.Ordinal), "the proxy does not reach demo-casino:8080 by name. " + casino.Describe());

        await AssertVariablesReachTheServicesAsync(deployment, containers, cancellationToken);
        await AssertContractVolumesExistAsync(deployment, cancellationToken);
    }

    [Fact(DisplayName = "Dado um compose sem um serviço do contrato SPEC-0065@2, quando o vps-deploy o lê, então recusa antes de subir e aponta o serviço")]
    [Trait("Category", "Contract")]
    [Trait("Spec", "SPEC-0066:CT-01")]
    public async Task GivenAComposeWithoutAContractServiceWhenDeployedThenItIsRefusedBeforeAnythingGoesUp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var deployment = await TestDeployment.CreateAsync(
            "ct01b", [new Release("release-broken", "A", WithoutService: "demo-casino")], cancellationToken);

        var deploy = await deployment.DeployAsync("release-broken", cancellationToken);

        Assert.False(deploy.Succeeded, "vps-deploy accepted a compose without demo-casino. " + deploy.Describe());
        Assert.True(deploy.ExitCode > 0, "vps-deploy did not run. " + deploy.Describe());
        Assert.Contains("demo-casino", deploy.Output, StringComparison.Ordinal);
        var containers = await DockerCli.ServiceContainersAsync(deployment.Project, cancellationToken);
        Assert.Empty(containers);
    }

    /// <summary>The variables of the .env reach the services as the compose of the contract maps them.</summary>
    private static async Task AssertVariablesReachTheServicesAsync(
        TestDeployment deployment, IReadOnlyDictionary<string, string> containers, CancellationToken cancellationToken)
    {
        var values = EnvFile.Read(deployment.EnvFilePath);
        foreach (var variable in DeployScripts.Secrets.Concat(DeployScripts.Settings))
        {
            Assert.True(values.TryGetValue(variable, out var value) && value.Length > 0, $".env without a value for {variable}");
        }

        var apiEnvironment = await EnvironmentOfAsync(containers["api"], cancellationToken).ConfigureAwait(false);
        Assert.Equal(values["OPERATORS_DEMO_KEY_ID"], apiEnvironment["Operators__Demo__KeyId"]);
        Assert.Equal(values["OPERATORS_DEMO_KEY_SECRET"], apiEnvironment["Operators__Demo__KeySecret"]);
        Assert.Equal(values["PLAYER_SESSION_SIGNING_KEY"], apiEnvironment["PlayerSession__SigningKey"]);
        Assert.Equal(values["BACKOFFICE_AUTHORITY"], apiEnvironment["Security__Backoffice__Authority"]);
        Assert.Equal(values["PUBLIC_API_URL"], apiEnvironment["PUBLIC_API_URL"]);
        Assert.Equal(values["PUBLIC_GAME_URL"], apiEnvironment["PUBLIC_GAME_URL"]);
        Assert.Contains("Password=" + values["POSTGRES_PASSWORD"] + ";", apiEnvironment["ConnectionStrings__apogeu"], StringComparison.Ordinal);
        Assert.Contains("password=" + values["REDIS_PASSWORD"], apiEnvironment["ConnectionStrings__redis"], StringComparison.Ordinal);
        var postgresEnvironment = await EnvironmentOfAsync(containers["postgres"], cancellationToken).ConfigureAwait(false);
        Assert.Equal(values["POSTGRES_PASSWORD"], postgresEnvironment["POSTGRES_PASSWORD"]);
    }

    /// <summary>The volumes of the contract exist in the project.</summary>
    private static async Task AssertContractVolumesExistAsync(TestDeployment deployment, CancellationToken cancellationToken)
    {
        var volumes = (await DockerCli.RunAsync(
                ["volume", "ls", "--filter", "label=com.docker.compose.project=" + deployment.Project, "--format", "{{.Name}}"],
                cancellationToken).ConfigureAwait(false)).StandardOutput;
        Assert.Contains($"{deployment.Project}_postgres-data", volumes, StringComparison.Ordinal);
        Assert.Contains($"{deployment.Project}_dataprotection-keys", volumes, StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyDictionary<string, string>> EnvironmentOfAsync(string container, CancellationToken cancellationToken)
    {
        var result = await DockerCli.RunAsync(["inspect", "--format", "{{range .Config.Env}}{{println .}}{{end}}", container], cancellationToken).ConfigureAwait(false);
        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.Ordinal);
    }
}
