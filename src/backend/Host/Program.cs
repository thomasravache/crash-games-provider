using Apogeu.Backoffice.Features;
using Apogeu.Betting.Features;
using Apogeu.Fairness.Features;
using Apogeu.Operators.Features;
using Apogeu.Rounds.Features;
using Apogeu.ServiceDefaults;
using Apogeu.Wallet.Features;

var builder = WebApplication.CreateBuilder(args);

// ADR-0003/ADR-0017: OpenTelemetry, health checks and resilience; the connection strings are required at startup
// (validated without connecting) and each dependency shows up in /health.
builder.AddServiceDefaults();
builder.AddPostgresDependency(connectionName: "apogeu", healthCheckName: "postgres");
builder.AddRedisDependency(connectionName: "redis", healthCheckName: "redis");

builder.Services.AddBackofficeModule();
builder.Services.AddBettingModule();
builder.Services.AddFairnessModule();
builder.Services.AddOperatorsModule();
builder.Services.AddRoundsModule();
builder.Services.AddWalletModule();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapBackofficeEndpoints();
app.MapBettingEndpoints();
app.MapFairnessEndpoints();
app.MapOperatorsEndpoints();
app.MapRoundsEndpoints();
app.MapWalletEndpoints();

await app.RunAsync().ConfigureAwait(false);
