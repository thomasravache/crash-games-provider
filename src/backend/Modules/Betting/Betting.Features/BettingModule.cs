using Apogeu.Betting.Features.CancelBet;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.PlaceBet;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Rounds.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Apogeu.Betting.Features;

/// <summary>Entry points the Host calls to compose the Betting module (ADR-0001).</summary>
public static class BettingModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>The sequential durable local queue of the Rounds integration events: one at a time, in order (IT-05).</summary>
    public const string RoundEventsQueue = "betting-round-events";

    /// <summary>The durable local queue of the reversals of uncertain debits and unconfirmed refunds.</summary>
    public const string WalletReversalsQueue = "betting-wallet-reversals";

    /// <summary>Cooldowns of the reversal retries; the last one repeats until the wallet confirms (ADR-0007).</summary>
    private static readonly TimeSpan[] ReversalRetries =
        [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    /// <summary>Cooldowns of a round event whose bets could not be updated (the database was unreachable).</summary>
    private static readonly TimeSpan[] RoundEventRetries =
        [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    /// <summary>
    /// Registers the hub <c>/hubs/bets</c>, its commands, the <see cref="IRoundFeed"/> fed by the Rounds integration events
    /// and the persistence of the schema <c>betting</c> on a pool of its own (<c>Betting:MaxPoolSize</c>); needs
    /// <c>IOperatorLimits</c> (Operators), <c>ISalaCaps</c> (Rounds), <c>IOperatorWallet</c> (Wallet), Wolverine
    /// (<see cref="ConfigureBettingMessaging"/>) and <see cref="TimeProvider"/> (Host).
    /// </summary>
    public static IServiceCollection AddBettingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<BettingOptions>().BindConfiguration(BettingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddMetrics();
        services.AddSingleton<BettingMetrics>();
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(BettingMetrics.MeterName));
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(BettingMetrics.MeterName));

        services.AddBettingInfrastructure(
            provider => provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."),
            provider => provider.GetRequiredService<IOptions<BettingOptions>>().Value.MaxPoolSize);
        services.AddHostedService<BettingDevelopmentMigrations>();

        services.AddSignalR();
        services.AddSingleton<IRoundFeed, RoundFeed>();
        services.AddSingleton<BetsNotifier>();
        services.AddScoped<PlaceBetUseCase>();
        services.AddScoped<CancelBetUseCase>();

        return services;
    }

    /// <summary>
    /// The module's messaging (ADR-0002): its handlers, the Rounds integration events routed to one sequential durable local
    /// queue (their order is kept) and the reversals of the wallet to a durable local queue retried until confirmed.
    /// </summary>
    public static WolverineOptions ConfigureBettingMessaging(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Discovery.IncludeAssembly(typeof(BettingModule).Assembly);

        options.PublishMessage<RoundBettingOpened>().ToLocalQueue(RoundEventsQueue);
        options.PublishMessage<RoundFlying>().ToLocalQueue(RoundEventsQueue);
        options.PublishMessage<RoundCrashed>().ToLocalQueue(RoundEventsQueue);
        options.PublishMessage<RoundAnnulled>().ToLocalQueue(RoundEventsQueue);
        options.LocalQueue(RoundEventsQueue).Sequential().UseDurableInbox();

        options.PublishMessage<ReverseBetDebit>().ToLocalQueue(WalletReversalsQueue);
        options.LocalQueue(WalletReversalsQueue).UseDurableInbox();

        options.OnException<WalletReversalPendingException>().RetryWithCooldown(ReversalRetries[..^1]).Then.ScheduleRetryIndefinitely(ReversalRetries[^1]);
        options.OnException<Npgsql.NpgsqlException>().RetryWithCooldown(RoundEventRetries);

        return options;
    }

    public static IEndpointRouteBuilder MapBettingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // SPEC-0009 (ADR-0015): denied by default, so the hub declares the player-session policy.
        endpoints.MapHub<BetsHub>(BetsHub.Path).RequireAuthorization(SecurityPolicies.PlayerSession);

        return endpoints;
    }
}
