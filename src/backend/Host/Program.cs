using Apogeu.Backoffice.Features;
using Apogeu.Betting.Features;
using Apogeu.Betting.Features.CashOut;
using Apogeu.Betting.Features.Settlement;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Messaging;
using Apogeu.Fairness.Features;
using Apogeu.Host.OpenApi;
using Apogeu.Host.Security;
using Apogeu.Operators.Features;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features;
using Apogeu.ServiceDefaults;
using Apogeu.ServiceDefaults.Messaging;
using Apogeu.Wallet.Features;
using Apogeu.Wallet.Infrastructure.Messaging;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OpenTelemetry.Metrics;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
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

// ADR-0002/ADR-0028: Wolverine for the handlers and the transactional outbox between modules, its envelopes in the schema
// `wolverine` of the same PostgreSQL — created at startup only in Development (local Aspire environment), like the modules'
// migrations; elsewhere by the pipeline (SPEC-0012). Only the Host configures it; its lifecycle is the Host's too: it starts
// once the database answers and the modules wait for IMessagingReadiness.
builder.UseWolverine(options =>
{
    var messagesConnectionString = builder.Configuration.GetConnectionString(RoundsModule.ConnectionName);
    if (!string.IsNullOrWhiteSpace(messagesConnectionString) && !BuildTimeDocument.IsGenerating)
    {
        options.PersistMessagesWithPostgresql(messagesConnectionString, "wolverine");
    }

    options.UseEntityFrameworkCoreTransactions();
    options.AutoBuildMessageStorageOnStartup = builder.Environment.IsDevelopment() ? AutoCreate.CreateOrUpdate : AutoCreate.None;
    options.Discovery.IncludeAssembly(typeof(BettingModule).Assembly);
    options.Discovery.IncludeAssembly(typeof(WalletModule).Assembly);

    // Every local queue is durable (the reversals of debits, ADR-0007); the round events of Rounds reach Betting through one
    // sequential queue, in the order the conductor published them (SPEC-0022 IT-05).
    options.Policies.UseDurableLocalQueues();
    options.PublishMessage<RoundBettingOpened>().ToLocalQueue(BettingModule.RoundEventsQueue);
    options.PublishMessage<RoundFlying>().ToLocalQueue(BettingModule.RoundEventsQueue);
    options.PublishMessage<RoundCrashed>().ToLocalQueue(BettingModule.RoundEventsQueue);
    options.PublishMessage<RoundAnnulled>().ToLocalQueue(BettingModule.RoundEventsQueue);

    // SPEC-0023: an automatic cash-out reaches Betting in order with the round events (after RoundFlying, before RoundCrashed).
    options.PublishMessage<AutoCashOutExecuted>().ToLocalQueue(BettingModule.RoundEventsQueue);
    options.LocalQueue(BettingModule.RoundEventsQueue).Sequential();

    // SPEC-0046: the resends of seamless credits and rollbacks to the operator wallets, one message per pending operation on a
    // wide queue of their own, so an operator that does not answer holds no other operator's pending operations.
    options.PublishMessage<ResendOperatorWallet>().ToLocalQueue(WalletModule.ResendQueue);
    options.LocalQueue(WalletModule.ResendQueue).MaximumParallelMessages(WalletModule.ResendParallelism);

    // A handler whose database is briefly unreachable retries before its message is given up.
    options.OnException<NpgsqlException>()
        .RetryWithCooldown(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

    // ADR-0028: the handlers' code is generated ahead of time into this assembly (Internal/Generated, `dotnet run --project
    // src/backend/Host -- codegen write`) and loaded at startup; nothing is compiled at runtime. The CI regenerates it and
    // fails when it differs from the commit (`codegen test` would compile it and needs the runtime compiler).
    options.ApplicationAssembly = typeof(Program).Assembly;
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
    options.CodeGeneration.GeneratedCodeOutputPath = Path.Combine(builder.Environment.ContentRootPath, "Internal", "Generated");

    // Service location only where the generated code cannot build a service itself (internal type or factory registration).
    options.ServiceLocationPolicy = ServiceLocationPolicy.AllowedButWarn;
    options.CodeGeneration.AlwaysUseServiceLocationFor<IBetStore>();
    options.CodeGeneration.AlwaysUseServiceLocationFor<ICashOutArbiter>();
    options.CodeGeneration.AlwaysUseServiceLocationFor<CashOutSettlement>();
    options.CodeGeneration.AlwaysUseServiceLocationFor<RoundSettler>();
});
MessagingLifecycle.AddMessagingLifecycle(builder.Services, RoundsModule.ConnectionName);
builder.Services.AddSingleton<IMessagingReadiness>(provider =>
{
    var lifecycle = provider.GetRequiredService<MessagingLifecycle>();
    return new MessagingReadiness(() => lifecycle.IsReady, lifecycle.WhenReadyAsync);
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
