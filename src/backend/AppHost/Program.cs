using Aspire.Hosting.JavaScript;

var builder = DistributedApplication.CreateBuilder(args);

// Credentials: none is written here. Aspire generates the postgres, redis and keycloak passwords as secret
// parameters and keeps them in the AppHost user secrets (ADR-0014), so the persistent volume keeps working.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var apogeuDatabase = postgres.AddDatabase("apogeu");

var redis = builder.AddRedis("redis");

builder.AddKeycloak("keycloak")
    .WithRealmImport("./Realms");

// Front-end apps (ADR-0004): one pnpm workspace, so a single install serves all of them.
const string FrontendApps = "../../frontend/apps";
var gamePipa = builder.AddViteApp("game-pipa", $"{FrontendApps}/game-pipa")
    .WithPnpm(install: true, installArgs: ["--frozen-lockfile"]);
var frontendInstall = gamePipa.Resource.Annotations.OfType<JavaScriptPackageInstallerAnnotation>().Single().Resource;

var api = builder.AddProject<Projects.Host>("api")
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health")
    .WithReference(apogeuDatabase)
    .WaitFor(apogeuDatabase)
    .WithReference(redis)
    .WaitFor(redis)
    // SPEC-0010: the game calls the API and negotiates the RoundsHub from its own origin (CORS of the Host).
    .WithEnvironment("Cors__GameOrigins__0", gamePipa.GetEndpoint("http"));

// SPEC-0010: base URL of the API for the game (read by Vite as import.meta.env.VITE_APOGEU_API_URL).
gamePipa.WithEnvironment("VITE_APOGEU_API_URL", api.GetEndpoint("http"));

builder.AddViteApp("backoffice", $"{FrontendApps}/backoffice")
    .WithPnpm(install: false)
    .WaitForCompletion(builder.CreateResourceBuilder(frontendInstall));

builder.AddViteApp("demo-site", $"{FrontendApps}/demo-site")
    .WithPnpm(install: false)
    .WaitForCompletion(builder.CreateResourceBuilder(frontendInstall));

await builder.Build().RunAsync().ConfigureAwait(false);
