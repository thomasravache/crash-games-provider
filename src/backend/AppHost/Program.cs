using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Credentials: none is written here. Aspire generates the postgres, redis and keycloak passwords as secret
// parameters and keeps them in the AppHost user secrets (ADR-0014), so the persistent volume keeps working.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var apogeuDatabase = postgres.AddDatabase("apogeu");

var redis = builder.AddRedis("redis");

var keycloak = builder.AddKeycloak("keycloak")
    .WithRealmImport("./Realms");

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
var isRunMode = builder.ExecutionContext.IsRunMode;
var gameHttpPort = isRunMode ? builder.Configuration.GetValue<int?>("AppHost:GameHttpPort") : null;
var publicApiUrl = isRunMode ? PublicUrl(builder.Configuration, "AppHost:PublicApiUrl") : null;
var publicGameUrl = isRunMode ? PublicUrl(builder.Configuration, "AppHost:PublicGameUrl") : null;

// Front-end apps (ADR-0004): one pnpm workspace, so a single install serves all of them.
const string FrontendApps = "../../frontend/apps";
var gamePipa = builder.AddViteApp("game-pipa", $"{FrontendApps}/game-pipa")
    .WithPnpm(install: true, installArgs: ["--frozen-lockfile"]);
if (gameHttpPort is { } port)
{
    gamePipa.WithEndpoint("http", endpoint => endpoint.Port = port);
}

var frontendInstall = gamePipa.Resource.Annotations.OfType<JavaScriptPackageInstallerAnnotation>().Single().Resource;

// SPEC-0010 (tools/ci/run-e2e.sh): the E2E runner picks a free port and passes it as AppHost__ApiHttpPort, so it knows the
// API URL without reading the dashboard. Unset (the normal local run and Aspire.Hosting.Testing), the port stays dynamic.
var apiHttpPort = builder.Configuration.GetValue<int?>("AppHost:ApiHttpPort");

var api = builder.AddProject<Projects.Host>("api")
    .WithHttpEndpoint(port: apiHttpPort)
    .WithHttpHealthCheck("/health")
    .WithReference(apogeuDatabase)
    .WaitFor(apogeuDatabase)
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("Operators__Demo__ApiKeys__0__Id", demoKeyId)
    .WithEnvironment("Operators__Demo__ApiKeys__0__Secret", demoKeySecret)
    .WithEnvironment("Security__PlayerSession__SigningKey", playerSessionSigningKey)
    .WithEnvironment("Security__Backoffice__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/apogeu"))
    .WithEnvironment("Security__Backoffice__Audience", "apogeu-api");

// SPEC-0010: the game calls the API and negotiates the RoundsHub from its own origin (CORS of the Host).
// SPEC-0050: with the public game address, the launch URL and the first allowed origin are the tunnel's, and the local
// origin stays allowed.
if (publicGameUrl is null)
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
if (publicApiUrl is null)
{
    gamePipa.WithEnvironment("VITE_APOGEU_API_URL", api.GetEndpoint("http"));
}
else
{
    gamePipa.WithEnvironment("VITE_APOGEU_API_URL", publicApiUrl);
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

builder.AddViteApp("backoffice", $"{FrontendApps}/backoffice")
    .WithPnpm(install: false)
    .WaitForCompletion(builder.CreateResourceBuilder(frontendInstall));

builder.AddViteApp("demo-site", $"{FrontendApps}/demo-site")
    .WithPnpm(install: false)
    .WaitForCompletion(builder.CreateResourceBuilder(frontendInstall));

await builder.Build().RunAsync().ConfigureAwait(false);

// An optional public address from the configuration, without a trailing slash (the Host appends the paths).
static string? PublicUrl(IConfiguration configuration, string key)
{
    var value = configuration[key]?.Trim().TrimEnd('/');
    return string.IsNullOrEmpty(value) ? null : value;
}
