using Apogeu.Backoffice.Features;
using Apogeu.Betting.Features;
using Apogeu.Fairness.Features;
using Apogeu.Host.OpenApi;
using Apogeu.Operators.Features;
using Apogeu.Rounds.Features;
using Apogeu.ServiceDefaults;
using Apogeu.Wallet.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

builder.Services.AddBackofficeModule();
builder.Services.AddBettingModule();
builder.Services.AddFairnessModule();
builder.Services.AddOperatorsModule();
builder.Services.AddRoundsModule();
builder.Services.AddWalletModule();

// SPEC-0010: the game runs on another origin (its own Vite app under Aspire, an operator domain later) and calls the
// REST API and the RoundsHub negotiation from the browser. Only the configured origins; no credentials, since the
// player session travels as a bearer/access_token, never as a cookie (SPEC-0009).
const string GameCorsPolicy = "game";
var gameOrigins = builder.Configuration.GetSection("Cors:GameOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(
    GameCorsPolicy,
    policy => policy.WithOrigins(gameOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors(GameCorsPolicy);

app.MapDefaultEndpoints();
app.MapContractOpenApi();

app.MapBackofficeEndpoints();
app.MapBettingEndpoints();
app.MapFairnessEndpoints();
app.MapOperatorsEndpoints();
app.MapRoundsEndpoints();
app.MapWalletEndpoints();

await app.RunAsync().ConfigureAwait(false);
