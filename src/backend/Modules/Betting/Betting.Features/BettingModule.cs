using Apogeu.Betting.Features.CancelBet;
using Apogeu.Betting.Features.CashOut;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Features.MyBets;
using Apogeu.Betting.Features.PlaceBet;
using Apogeu.Betting.Features.Reservations;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Settlement;
using Apogeu.Betting.Infrastructure;
using Apogeu.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Apogeu.Betting.Features;

/// <summary>Entry points the Host calls to compose the Betting module (ADR-0001).</summary>
public static class BettingModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>
    /// The local queue the Host routes the Rounds integration events to (ADR-0028): durable and sequential, so Betting
    /// handles them one at a time and in order (IT-05).
    /// </summary>
    public const string RoundEventsQueue = "betting-round-events";

    /// <summary>
    /// Registers the hub <c>/hubs/bets</c>, its commands, the <see cref="IRoundFeed"/> fed by the Rounds integration events,
    /// the sweeper of pending reservations and the persistence of the schema <c>betting</c> on a pool of its own
    /// (<c>Betting:MaxPoolSize</c>); needs <c>IOperatorLimits</c> (Operators), <c>ISalaCaps</c> and <c>ISalaPhase</c>
    /// (Rounds), <c>IOperatorWallet</c> (Wallet), the Host's messaging (Wolverine, <c>IMessagingReadiness</c>, ADR-0028)
    /// and <see cref="TimeProvider"/> (Host).
    /// </summary>
    public static IServiceCollection AddBettingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<BettingOptions>().BindConfiguration(BettingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<BettingOptions>, BettingOptionsValidator>();
        services.AddMetrics();
        services.AddSingleton<BettingMetrics>();
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(BettingMetrics.MeterName));
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(BettingMetrics.MeterName));

        services.AddBettingInfrastructure(
            provider => provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."),
            provider => provider.GetRequiredService<IOptions<BettingOptions>>().Value.MaxPoolSize);
        services.AddHostedService<BettingDevelopmentMigrations>();
        services.AddHostedService<PendingReservationSweeper>();

        services.AddSignalR();
        services.AddSingleton<IRoundFeed, RoundFeed>();
        services.AddSingleton<BetsNotifier>();

        // SPEC-0025: the read projection of the live bets of each room and its broadcast every 200 ms.
        services.AddSingleton<LiveBetsBatcher>();
        services.AddSingleton<LiveBetsFeed>();
        services.AddHostedService<LiveBetsBroadcaster>();
        services.AddScoped<PlaceBetUseCase>();
        services.AddScoped<CancelBetUseCase>();
        services.AddScoped<CashOutUseCase>();
        services.AddScoped<CashOutSettlement>();
        services.AddScoped<RoundSettler>();

        return services;
    }

    public static IEndpointRouteBuilder MapBettingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // SPEC-0009 (ADR-0015): denied by default, so the hub declares the player-session policy.
        endpoints.MapHub<BetsHub>(BetsHub.Path).RequireAuthorization(SecurityPolicies.PlayerSession);
        endpoints.MapGetMyBets();

        return endpoints;
    }
}
