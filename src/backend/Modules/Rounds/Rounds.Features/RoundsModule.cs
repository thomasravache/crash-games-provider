using Apogeu.BuildingBlocks.Security;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Rounds.Features.History;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Features.Salas;
using Apogeu.Rounds.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Apogeu.Rounds.Features;

/// <summary>Entry points the Host calls to compose the Rounds module (ADR-0001).</summary>
public static class RoundsModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>Prefix of the SignalR backplane channels in Redis.</summary>
    private const string BackplaneChannelPrefix = "apogeu:signalr";

    /// <summary>
    /// Registers the rooms (<c>Rounds:Salas:{id}</c>, validated at startup), the persistence of the schema <c>rounds</c>
    /// (migrated at startup in Development only, ADR-0010), the hub with the Redis backplane and the leader election that
    /// runs the conductor of each room (ADR-0005). Called after the Host's <c>UseWolverine</c>: the conductor publishes the
    /// round events through Wolverine's transactional outbox (SPEC-0022), whose runtime starts with the module.
    /// </summary>
    public static IServiceCollection AddRoundsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RoundsOptions>().BindConfiguration(RoundsOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<RoundsOptions>, RoundsOptionsValidator>();
        services.AddSingleton<SalaCatalog>();
        services.AddSingleton<ISalaCaps, SalaCaps>();
        services.AddMetrics();
        services.AddSingleton<RoundsMetrics>();

        // The module's meter and activity source reach the Host's OpenTelemetry pipeline (ADR-0017, SPEC-0008 §8).
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(RoundsMetrics.MeterName));
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(RoundsMetrics.MeterName));

        // The module's own Npgsql pool is bounded (Rounds:MaxPoolSize), so no burst opens more connections than that (IMP-02).
        services.AddRoundsInfrastructure(
            provider => provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."),
            provider => provider.GetRequiredService<IOptions<RoundsOptions>>().Value.MaxPoolSize);
        services.AddHostedService<RoundsDevelopmentMigrations>();

        // The hub and the REST endpoints require a player session; the policy and its scheme come from SPEC-0009.
        services.AddAuthorization();
        services.AddSignalR().AddStackExchangeRedis();
        services.AddOptions<RedisOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.Configuration = RoundsRedis.Options(configuration);
            options.Configuration.ChannelPrefix = RedisChannel.Literal(BackplaneChannelPrefix);
        });
        services.AddSingleton<IRoundBroadcaster, SignalRRoundBroadcaster>();
        services.AddSingleton<SalaLiveViews>();
        services.AddSingleton(provider => new SalaSnapshotQuery(
            provider.GetRequiredService<SalaCatalog>(),
            provider.GetRequiredService<SalaLiveViews>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<RoundsOptions>>().Value.SnapshotReadTimeout));

        // Wolverine's runtime starts right before the conductor that publishes through its outbox (ADR-0002, SPEC-0022).
        RoundsMessagingStartup.TakeOver(services);

        services.AddSingleton<RoundsRedis>();
        services.AddSingleton<RoundsLeadershipService>();
        services.AddSingleton<IRoundsLeadership>(provider => provider.GetRequiredService<RoundsLeadershipService>());
        services.AddHostedService(provider => provider.GetRequiredService<RoundsLeadershipService>());

        return services;
    }

    public static IEndpointRouteBuilder MapRoundsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // SPEC-0009 (ADR-0015): denied by default, so every endpoint of the module declares the player-session policy.
        endpoints.MapHub<RoundsHub>(RoundsHub.Path).RequireAuthorization(SecurityPolicies.PlayerSession);
        endpoints.MapGetSalaRounds();
        endpoints.MapGetRoundVerification();

        return endpoints;
    }
}
