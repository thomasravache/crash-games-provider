using System.Text.RegularExpressions;
using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Unit;

/// <summary>
/// SPEC-0065: the Docker Compose the AppHost publishes for production (ADR-0029), read from the files <c>aspire publish</c>
/// writes. The publish only describes the system: no container, image or port is used.
/// </summary>
[Collection(PublishedComposeGroup.Name)]
public sealed partial class ComposePublishTests(PublishedComposeFixture published)
{
    private const string Api = "api";
    private const string DemoCasino = "demo-casino";
    private const string KeysPath = "/keys";
    private static readonly string[] ContractServices = [Api, DemoCasino, "postgres", "redis"];

    [Fact(DisplayName = "Dado o AppHost em modo publish, quando o compose é gerado, então tem api, demo-casino, postgres e redis (sem keycloak), os volumes de dados e de chaves, ambiente Production, limites de memória, nenhuma porta publicada no host e nenhum segredo literal")]
    [Trait("Spec", "SPEC-0065:UT-01")]
    public void GivenAppHostInPublishModeWhenComposeIsGeneratedThenItIsTheProductionComposeWithoutPortsOrSecrets()
    {
        published.AssertPublished();
        var compose = ComposeDocument.Load(published.ComposePath);

        var missing = ContractServices.Where(service => !compose.HasService(service)).ToList();
        Assert.True(
            missing.Count == 0,
            $"The compose lacks the services {string.Join(", ", missing)}. Services: {string.Join(", ", compose.ServiceNames)}.");

        // Emenda v2: the Keycloak stays out of production until the backoffice is published (SPEC-0036).
        Assert.False(compose.HasService("keycloak"), "The production compose must not run the keycloak (Emenda v2).");

        // Data volumes: the PostgreSQL data and the Data Protection key ring of the api (DataProtection:KeysPath=/keys, §6).
        Assert.Contains("postgres-data", compose.VolumeNames, StringComparer.Ordinal);
        Assert.Contains("dataprotection-keys", compose.VolumeNames, StringComparer.Ordinal);
        Assert.Contains(compose.Mounts("postgres"), mount => string.Equals(mount.Source, "postgres-data", StringComparison.Ordinal));
        Assert.Contains(compose.Mounts(Api), mount => string.Equals(mount.Source, "dataprotection-keys", StringComparison.Ordinal) && string.Equals(mount.Target, KeysPath, StringComparison.Ordinal));
        Assert.Equal(KeysPath, Assert.Contains("DataProtection__KeysPath", compose.Environment(Api)));

        foreach (var service in new[] { Api, DemoCasino })
        {
            Assert.True(
                compose.Environment(service).TryGetValue("ASPNETCORE_ENVIRONMENT", out var environment) && string.Equals(environment, "Production", StringComparison.Ordinal),
                $"The service '{service}' must run with ASPNETCORE_ENVIRONMENT=Production, found '{environment}'.");
        }

        foreach (var service in compose.ServiceNames)
        {
            Assert.True(compose.MemoryLimit(service) is not null, $"The service '{service}' has no memory limit (deploy.resources.limits.memory).");
            var ports = compose.Ports(service);
            Assert.True(ports.Count == 0, $"The service '{service}' publishes ports on the host ({string.Join(", ", ports)}): only the proxy of SPEC-0066 may.");
        }

        AssertNoLiteralSecrets(compose);
    }

    [Fact(DisplayName = "Dados os parâmetros public-game-url e public-api-url, quando o compose é gerado, então a API recebe Operators__GameBaseUrl e Cors__GameOrigins__0 com o endereço público do jogo e a carteira seamless do operador demo aponta para http://demo-casino com AllowHttpHosts__0=demo-casino")]
    [Trait("Spec", "SPEC-0065:UT-02")]
    public void GivenPublicAddressesWhenComposeIsGeneratedThenApiUsesThePublicGameAddressAndTheSeamlessWalletIsTheDemoCasino()
    {
        published.AssertPublished();
        var api = ComposeDocument.Load(published.ComposePath).Environment(Api);

        // The public address comes from the .env of the host (PUBLIC_GAME_URL, §6), never baked into the compose.
        Assert.Equal("${PUBLIC_GAME_URL}", Assert.Contains("Operators__GameBaseUrl", api));
        Assert.Equal("${PUBLIC_GAME_URL}", Assert.Contains("Cors__GameOrigins__0", api));

        // §6 (Emenda v2): the authority of the backoffice tokens is a parameter too, not a keycloak of the compose.
        Assert.Equal("${BACKOFFICE_AUTHORITY}", Assert.Contains("Security__Backoffice__Authority", api));

        // The demo operator is seamless and its wallet is the DemoCasino on the internal network (SPEC-0047), signed with
        // the demo operator key of the .env.
        Assert.Equal("Seamless", Assert.Contains("Wallet__Operators__demo__Mode", api));
        var walletUrl = Assert.Contains("Wallet__Operators__demo__WalletUrl", api);
        Assert.Matches(DemoCasinoUrl(), walletUrl);
        Assert.Equal(DemoCasino, Assert.Contains("Wallet__Operators__demo__AllowHttpHosts__0", api));
        Assert.Equal("${OPERATORS_DEMO_KEY_ID}", Assert.Contains("Wallet__Operators__demo__KeyId", api));
        Assert.Equal("${OPERATORS_DEMO_KEY_SECRET}", Assert.Contains("Wallet__Operators__demo__Secret", api));
    }

    /// <summary>
    /// No secret value in the generated files: the canary values given to the AppHost appear in none of them; every
    /// credential-like variable and every password inside a connection string or URI is only a <c>${VARIABLE}</c>
    /// reference; and the .env carries no value at all.
    /// </summary>
    private void AssertNoLiteralSecrets(ComposeDocument compose)
    {
        foreach (var file in published.Files())
        {
            var text = File.ReadAllText(Path.Combine(published.OutputDirectory, file));
            foreach (var (parameter, value) in published.Canaries)
            {
                Assert.False(text.Contains(value, StringComparison.Ordinal), $"{file} carries the value of the secret parameter '{parameter}'.");
            }
        }

        foreach (var service in compose.ServiceNames)
        {
            foreach (var (name, value) in compose.Environment(service))
            {
                if (CredentialName().IsMatch(name))
                {
                    Assert.True(
                        OnlyReference().IsMatch(value),
                        $"{service}: {name} must be a ${{VARIABLE}} reference, found a literal value.");
                }

                foreach (Match password in EmbeddedPassword().Matches(value))
                {
                    Assert.True(
                        OnlyReference().IsMatch(password.Groups["value"].Value),
                        $"{service}: {name} embeds a literal password.");
                }
            }
        }

        var envTemplate = published.EnvTemplatePath();
        var withValue = EnvFile.Read(envTemplate).Where(variable => variable.Value.Length > 0).Select(variable => variable.Key).ToList();
        Assert.True(
            withValue.Count == 0,
            $"{Path.GetFileName(envTemplate)} must carry no values; it sets {string.Join(", ", withValue)}.");
    }

    /// <summary>Plain http to the DemoCasino service by name (port 8080 of §6 or the default), any path.</summary>
    [GeneratedRegex(@"^http://demo-casino(:8080)?(/.*)?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DemoCasinoUrl();

    [GeneratedRegex(@"(password|passwd|pwd|secret|signing_?key|api_?key|token)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CredentialName();

    /// <summary>A password inside a connection string (<c>Password=…;</c>) or the user info of a URI (<c>://user:…@</c>).</summary>
    [GeneratedRegex(@"(?:(?:password|pwd)=(?<value>[^;,]*))|(?:://[^:/@\s]*:(?<value>[^@\s]+)@)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmbeddedPassword();

    [GeneratedRegex(@"^\$\{[A-Za-z_][A-Za-z0-9_]*\}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OnlyReference();
}
