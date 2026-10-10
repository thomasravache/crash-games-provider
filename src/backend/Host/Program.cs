using Apogeu.Backoffice.Features;
using Apogeu.Betting.Features;
using Apogeu.Fairness.Features;
using Apogeu.Host.OpenApi;
using Apogeu.Host.Security;
using Apogeu.Operators.Features;
using Apogeu.Rounds.Features;
using Apogeu.ServiceDefaults;
using Apogeu.Wallet.Features;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation starts the Host without deployment configuration (no-op in a real run).
builder.UseBuildTimeDocumentConfiguration();

// ADR-0003/ADR-0017: OpenTelemetry, health checks and resilience; the connection strings are required at startup
// (validated without connecting) and each dependency shows up in /health.
builder.AddServiceDefaults();
builder.AddPostgresDependency(connectionName: "apogeu", healthCheckName: "postgres");
builder.AddRedisDependency(connectionName: "redis", healthCheckName: "redis");

// ADR-0008: contract JSON conventions on the HTTP APIs and the OpenAPI document the TypeScript contracts come from.
builder.Services.AddContractOpenApi();

// Clock shared by every module (tests replace it); registered by the Host, not by a module.
builder.Services.TryAddSingleton(TimeProvider.System);

// ADR-0002: Wolverine for the handlers and the transactional outbox between modules, its envelopes in the schema
// `wolverine` of the same PostgreSQL. Its tables are created at startup only in Development (local Aspire environment),
// like the modules' migrations; elsewhere the deployment pipeline creates them (ADR-0010). Registered before the modules:
// the Rounds module starts its runtime right before the conductor that publishes through its outbox.
builder.UseWolverine(options =>
{
    var messagesConnectionString = builder.Configuration.GetConnectionString(RoundsModule.ConnectionName);
    if (!string.IsNullOrWhiteSpace(messagesConnectionString) && !BuildTimeDocument.IsGenerating)
    {
        options.PersistMessagesWithPostgresql(messagesConnectionString, "wolverine");
    }

    options.UseEntityFrameworkCoreTransactions();
    options.AutoBuildMessageStorageOnStartup = builder.Environment.IsDevelopment() ? AutoCreate.CreateOrUpdate : AutoCreate.None;
    options.ConfigureBettingMessaging();

    // The handlers' code is generated ahead of time into Betting.Features (`dotnet run --project src/backend/Host --
    // codegen write`) and loaded at startup; nothing is compiled at runtime.
    options.ApplicationAssembly = typeof(BettingModule).Assembly;
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;

    // The modules' services stay internal (or are registered by factory): the generated handlers resolve them from the
    // container, so every registration (including the tests' replacements) is honoured.
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.GeneratedCodeOutputPath = Path.GetFullPath(
        Path.Combine(builder.Environment.ContentRootPath, "..", "Modules", "Betting", "Betting.Features", "Internal", "Generated"));
});

builder.Services.AddBackofficeModule();
builder.Services.AddBettingModule();
builder.Services.AddFairnessModule();
builder.Services.AddOperatorsModule();
builder.Services.AddRoundsModule();
builder.Services.AddWalletModule();

// SPEC-0009, ADR-0015: authorization denied by default, security headers and HSTS; the identities (operator signature,
// player session, backoffice bearer) and their policies are registered by the Operators module above.
builder.Services.AddHostSecurity();
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(OperatorsModule.MeterName));

// SPEC-0010: the game runs on another origin (its own Vite app under Aspire, an operator domain later) and calls the
// REST API and the RoundsHub negotiation from the browser. Only the configured origins; no credentials, since the
// player session travels as a bearer/access_token, never as a cookie (SPEC-0009).
const string GameCorsPolicy = "game";
var gameOrigins = builder.Configuration.GetSection("Cors:GameOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(
    GameCorsPolicy,
    policy => policy.WithOrigins(gameOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// CORS before authentication/authorization: preflights and the hub negotiation get the CORS headers even when denied.
app.UseCors(GameCorsPolicy);
app.UseHostSecurity();

app.MapDefaultEndpoints();
app.MapContractOpenApi();

app.MapBackofficeEndpoints();
app.MapBettingEndpoints();
app.MapFairnessEndpoints();
app.MapOperatorsEndpoints();
app.MapRoundsEndpoints();
app.MapWalletEndpoints();

// `codegen write|test|preview` (JasperFx) regenerates the Wolverine handlers' code; any other run starts the Host.
if (args is ["codegen", ..])
{
    return await app.RunJasperFxCommands(args).ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);
return 0;
