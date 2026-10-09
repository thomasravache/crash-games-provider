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

// Front-end apps (ADR-0004): one pnpm workspace, so a single install serves all of them.
const string FrontendApps = "../../frontend/apps";
var gamePipa = builder.AddViteApp("game-pipa", $"{FrontendApps}/game-pipa")
    .WithPnpm(install: true, installArgs: ["--frozen-lockfile"]);
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
    .WithEnvironment("Operators__GameBaseUrl", gamePipa.GetEndpoint("http"))
    .WithEnvironment("Operators__Demo__ApiKeys__0__Id", demoKeyId)
    .WithEnvironment("Operators__Demo__ApiKeys__0__Secret", demoKeySecret)
    .WithEnvironment("Security__PlayerSession__SigningKey", playerSessionSigningKey)
    .WithEnvironment("Security__Backoffice__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/apogeu"))
    .WithEnvironment("Security__Backoffice__Audience", "apogeu-api")
    // SPEC-0010: the game calls the API and negotiates the RoundsHub from its own origin (CORS of the Host).
    .WithEnvironment("Cors__GameOrigins__0", gamePipa.GetEndpoint("http"));

// SPEC-0010: base URL of the API for the game (read by Vite as import.meta.env.VITE_APOGEU_API_URL).
gamePipa.WithEnvironment("VITE_APOGEU_API_URL", api.GetEndpoint("http"));

// Local run only: never baked into a publish (deploy output).
if (builder.ExecutionContext.IsRunMode)
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
