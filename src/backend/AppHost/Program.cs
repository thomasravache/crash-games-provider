using Apogeu.AppHost;
using Aspire.Hosting.Docker;
using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// The same AppHost describes the local run and the production publish (ADR-0003, ADR-0029): `aspire publish` writes a
// Docker Compose (docker-compose.yaml and a .env without values, SPEC-0065). What is only for development (Vite apps,
// Keycloak, the dashboard, the dev wallet wiring) stays in run mode.
var isRunMode = builder.ExecutionContext.IsRunMode;
var isPublishMode = builder.ExecutionContext.IsPublishMode;

// Credentials: none is written here. Aspire generates the postgres, redis and keycloak passwords as secret
// parameters and keeps them in the AppHost user secrets (ADR-0014), so the persistent volume keeps working. In the published
// compose they are only ${VARIABLE} references filled by the .env of the host (POSTGRES_PASSWORD, REDIS_PASSWORD).
var postgres = builder.AddPostgres("postgres");
if (isPublishMode)
{
    postgres.WithDataVolume(ProductionCompose.PostgresVolume)
        .PublishAsDockerComposeService((_, service) => ProductionCompose.LimitMemory(service, "512M"));
}
else
{
    postgres.WithDataVolume();
}

var apogeuDatabase = postgres.AddDatabase("apogeu");

var redis = builder.AddRedis("redis");
if (isPublishMode)
{
    redis.PublishAsDockerComposeService((_, service) => ProductionCompose.LimitMemory(service, "128M"));
}

// SPEC-0065 (Emenda v2): the Keycloak (backoffice identity) runs only locally; in production the authority of the
// backoffice tokens is the parameter backoffice-authority, until the backoffice is published (SPEC-0036).
var keycloak = isRunMode
    ? builder.AddKeycloak("keycloak").WithRealmImport("./Realms")
    : null;

// Security settings of the Host (SPEC-0009, ADR-0014/ADR-0015): generated on the first run and kept in the AppHost user
// secrets (persist), never written here. The demo operator key id is not a secret but is generated the same way, so every
// developer has their own. The session signing key is read as base64: 64 alphanumeric characters are valid base64 of
// 48 bytes (≥ 32 required).
var demoKeyId = builder.AddParameter(
    "operators-demo-key-id",
    new GenerateParameterDefault { MinLength = 16, Special = false, Upper = false },
    secret: false,
    persist: true);
var demoKeySecret = builder.AddParameter(
    "operators-demo-key-secret",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);
var playerSessionSigningKey = builder.AddParameter(
    "player-session-signing-key",
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true,
    persist: true);

// SPEC-0050: optional addresses to open the local game on a phone through two tunnels (cloudflared), local run only.
// GameHttpPort fixes the port of the game so a tunnel can point at it; PublicApiUrl and PublicGameUrl are the public
// addresses of the tunnels. Unset (the normal local run), the API and the game only know each other's local endpoints.
var gameHttpPort = isRunMode ? builder.Configuration.GetValue<int?>("AppHost:GameHttpPort") : null;
var publicApiUrl = isRunMode ? PublicUrl(builder.Configuration, "AppHost:PublicApiUrl") : null;
var publicGameUrl = isRunMode ? PublicUrl(builder.Configuration, "AppHost:PublicGameUrl") : null;

// Front-end apps (ADR-0004): one pnpm workspace, so a single install serves all of them. Local run only: in production
// the static builds are served by the proxy (SPEC-0066), outside the compose.
const string FrontendApps = "../../frontend/apps";
IResourceBuilder<ViteAppResource>? gamePipa = null;
IResource? frontendInstall = null;
if (isRunMode)
{
    gamePipa = builder.AddViteApp("game-pipa", $"{FrontendApps}/game-pipa")
        .WithPnpm(install: true, installArgs: ["--frozen-lockfile"]);
    if (gameHttpPort is { } port)
    {
        gamePipa.WithEndpoint("http", endpoint => endpoint.Port = port);
    }

    frontendInstall = gamePipa.Resource.Annotations.OfType<JavaScriptPackageInstallerAnnotation>().Single().Resource;
}

// SPEC-0010 (tools/ci/run-e2e.sh): the E2E runner picks a free port and passes it as AppHost__ApiHttpPort, so it knows the
// API URL without reading the dashboard. Unset (the normal local run and Aspire.Hosting.Testing), the port stays dynamic.
// In the published compose the api listens on 8080 inside the network, reached by name by the proxy (SPEC-0066).
var apiHttpPort = builder.Configuration.GetValue<int?>("AppHost:ApiHttpPort");

var api = builder.AddProject<Projects.Host>("api");
if (isPublishMode)
{
    api.WithHttpEndpoint(targetPort: ProductionCompose.ServicePort);
}
else
{
    api.WithHttpEndpoint(port: apiHttpPort);
}

api.WithHttpHealthCheck("/health")
    .WithReference(apogeuDatabase)
    .WaitFor(apogeuDatabase)
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("Operators__Demo__ApiKeys__0__Id", demoKeyId)
    .WithEnvironment("Operators__Demo__ApiKeys__0__Secret", demoKeySecret)
    .WithEnvironment("Security__PlayerSession__SigningKey", playerSessionSigningKey)
    .WithEnvironment("Security__Backoffice__Audience", "apogeu-api");
if (keycloak is not null)
{
    api.WithEnvironment("Security__Backoffice__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/apogeu"));
}
else
{
    api.WithEnvironment("Security__Backoffice__Authority", builder.AddParameter("backoffice-authority"));
}

// SPEC-0010: the game calls the API and negotiates the RoundsHub from its own origin (CORS of the Host).
// SPEC-0050: with the public game address, the launch URL and the first allowed origin are the tunnel's, and the local
// origin stays allowed. SPEC-0065: in production the game is only at its public address (parameter public-game-url).
if (gamePipa is null)
{
    var productionGameUrl = builder.AddParameter("public-game-url");
    api.WithEnvironment("Operators__GameBaseUrl", productionGameUrl)
        .WithEnvironment("Cors__GameOrigins__0", productionGameUrl);
}
else if (publicGameUrl is null)
{
    api.WithEnvironment("Operators__GameBaseUrl", gamePipa.GetEndpoint("http"))
        .WithEnvironment("Cors__GameOrigins__0", gamePipa.GetEndpoint("http"));
}
else
{
    api.WithEnvironment("Operators__GameBaseUrl", publicGameUrl)
        .WithEnvironment("Cors__GameOrigins__0", publicGameUrl)
        .WithEnvironment("Cors__GameOrigins__1", gamePipa.GetEndpoint("http"));
}

// SPEC-0010: base URL of the API for the game (read by Vite as import.meta.env.VITE_APOGEU_API_URL); SPEC-0050: the
// public API address when given.
if (gamePipa is not null)
{
    if (publicApiUrl is null)
    {
        gamePipa.WithEnvironment("VITE_APOGEU_API_URL", api.GetEndpoint("http"));
    }
    else
    {
        gamePipa.WithEnvironment("VITE_APOGEU_API_URL", publicApiUrl);
    }
}

// Local run only: never baked into a publish (deploy output).
if (isRunMode)
{
    // The Host runs in the environment of the AppHost (Development locally), so the Development-only pieces of SPEC-0009
    // (migration and seed of the demo operator, /dev/launch-demo) are there under Aspire; and the local Keycloak may be
    // served over plain HTTP, so its metadata is not required to come over HTTPS.
    api.WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
        .WithEnvironment("Security__Backoffice__RequireHttpsMetadata", "false");
}

if (frontendInstall is not null)
{
    builder.AddViteApp("backoffice", $"{FrontendApps}/backoffice")
        .WithPnpm(install: false)
        .WaitForCompletion(builder.CreateResourceBuilder(frontendInstall));
}

// Demo casino (SPEC-0043, ADR-0027): an external operator of the Apogeu. The DemoCasino BFF signs its calls with the demo
// operator key of SPEC-0009 (the same parameters the api seeds), and only it receives them; the demo-site gets just the
// DemoCasino address, read by its Vite server (no VITE_ prefix, so never in the bundle) to forward /casino there.
// Referenced by path (the DemoCasino is not a project of the Apogeu): Aspire runs it with --no-build, so it is built by
// `dotnet build Apogeu.slnx` (or tools/ci/run-e2e.sh). Nothing waits for it: the Vite server only forwards /casino.
var demoCasino = builder.AddProject("demo-casino", "../../demo/DemoCasino/DemoCasino.csproj");
if (isPublishMode)
{
    demoCasino.WithHttpEndpoint(targetPort: ProductionCompose.ServicePort);
}
else
{
    demoCasino.WithHttpEndpoint();
}

demoCasino.WithHttpHealthCheck("/health")
    .WithEnvironment("Apogeu__BaseUrl", api.GetEndpoint("http"))
    .WithEnvironment("Apogeu__OperatorKeyId", demoKeyId)
    .WithEnvironment("Apogeu__OperatorSecret", demoKeySecret);

// SPEC-0047 (Emenda v1): in Development the demo operator is seamless and its wallet is the DemoCasino (public wallet
// contract, SPEC-0021@4): the api calls the DemoCasino endpoint (plain http to the loopback is accepted), signed with the
// demo operator key the casino already holds (Wallet:Operators:demo:*, the provisional settings of SPEC-0046). Endpoint
// references only, no WaitFor: the DemoCasino depends on the api address, and a wallet call made before it is up is
// uncertain like any other (resent or refused by the api).
if (isRunMode && builder.Environment.IsDevelopment())
{
    api.WithEnvironment("Wallet__Operators__demo__Mode", "Seamless")
        .WithEnvironment("Wallet__Operators__demo__WalletUrl", demoCasino.GetEndpoint("http"))
        .WithEnvironment("Wallet__Operators__demo__KeyId", demoKeyId)
        .WithEnvironment("Wallet__Operators__demo__Secret", demoKeySecret);
}

// SPEC-0043 (tools/ci/run-e2e.sh): the E2E runner fixes the demo-site port through AppHost__DemoSiteHttpPort, as for the
// API; unset, the port stays dynamic.
if (frontendInstall is not null)
{
    var demoSiteHttpPort = builder.Configuration.GetValue<int?>("AppHost:DemoSiteHttpPort");

    var demoSite = builder.AddViteApp("demo-site", $"{FrontendApps}/demo-site")
        .WithPnpm(install: false)
        .WaitForCompletion(builder.CreateResourceBuilder(frontendInstall))
        .WithEnvironment("DEMO_CASINO_URL", demoCasino.GetEndpoint("http"));
    if (demoSiteHttpPort is { } demoSitePort)
    {
        demoSite.WithEndpoint("http", endpoint => endpoint.Port = demoSitePort);
    }
}

// SPEC-0065 (ADR-0029): the production compose. The api and the DemoCasino run in Production; the api migrates every module
// and ensures the demo operator on its start; the demo operator is seamless with its wallet in the DemoCasino, over plain
// http on the internal network (the only listed host); the Data Protection keys live in a volume, handed to the api user by
// keys-init before the api starts (Emenda v3); every service has a memory
// limit and none publishes a port on the host (only the proxy of SPEC-0066 does). The secrets and the addresses are
// ${VARIABLE} references filled by the .env of the host.
if (isPublishMode)
{
    var publicApiAddress = builder.AddParameter("public-api-url");

    builder.AddDockerComposeEnvironment("compose")
        .WithDashboard(false)
        .ConfigureComposeFile(file =>
        {
            file.AddVolume(ProductionCompose.KeysVolumeDeclaration());
            file.AddService(ProductionCompose.KeysInit());
        })

        // PUBLIC_API_URL is read by the proxy and the static build of the game (SPEC-0066), not by a service of the compose.
        .ConfigureEnvFile(variables => variables[ProductionCompose.PublicApiUrlVariable] = new CapturedEnvironmentVariable
        {
            Name = ProductionCompose.PublicApiUrlVariable,
            Description = "Public address of the API (proxy and static build of the game, SPEC-0066).",
            Source = publicApiAddress.Resource,
        });

    api.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
        .WithEnvironment("Database__MigrateOnStartup", "true")
        .WithEnvironment("Operators__Demo__Enabled", "true")
        .WithEnvironment("DataProtection__KeysPath", ProductionCompose.KeysPath)
        .WithEnvironment("Wallet__Operators__demo__Mode", "Seamless")
        .WithEnvironment("Wallet__Operators__demo__WalletUrl", demoCasino.GetEndpoint("http"))
        .WithEnvironment("Wallet__Operators__demo__AllowHttpHosts__0", demoCasino.Resource.Name)
        .WithEnvironment("Wallet__Operators__demo__KeyId", demoKeyId)
        .WithEnvironment("Wallet__Operators__demo__Secret", demoKeySecret)
        .PublishAsDockerComposeService((_, service) =>
        {
            ProductionCompose.LimitMemory(service, "768M");
            service.AddVolume(ProductionCompose.KeysVolumeMount());
            ProductionCompose.AfterKeysInit(service);
        });

    demoCasino.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
        .PublishAsDockerComposeService((_, service) => ProductionCompose.LimitMemory(service, "256M"));
}

await builder.Build().RunAsync().ConfigureAwait(false);

// An optional public address from the configuration, without a trailing slash (the Host appends the paths).
static string? PublicUrl(IConfiguration configuration, string key)
{
    var value = configuration[key]?.Trim().TrimEnd('/');
    return string.IsNullOrEmpty(value) ? null : value;
}
