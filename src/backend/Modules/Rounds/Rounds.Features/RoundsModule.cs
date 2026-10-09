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
    /// runs the conductor of each room (ADR-0005).
    /// </summary>
    public static IServiceCollection AddRoundsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<RoundsOptions>().BindConfiguration(RoundsOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<RoundsOptions>, RoundsOptionsValidator>();
        services.AddSingleton<SalaCatalog>();
        services.AddMetrics();
        services.AddSingleton<RoundsMetrics>();

        services.AddRoundsInfrastructure(provider =>
            provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."));
        services.AddHostedService<RoundsDevelopmentMigrations>();

        // The hub requires an authenticated user, with the scheme the Host registers (SPEC-0009).
        services.AddAuthorization();
        services.AddSignalR().AddStackExchangeRedis();
        services.AddOptions<RedisOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.Configuration = RoundsRedis.Options(configuration);
            options.Configuration.ChannelPrefix = RedisChannel.Literal(BackplaneChannelPrefix);
        });
        services.AddSingleton<IRoundBroadcaster, SignalRRoundBroadcaster>();
        services.AddScoped<SalaSnapshotQuery>();

        services.AddSingleton<RoundsRedis>();
        services.AddSingleton<RoundsLeadershipService>();
        services.AddSingleton<IRoundsLeadership>(provider => provider.GetRequiredService<RoundsLeadershipService>());
        services.AddHostedService(provider => provider.GetRequiredService<RoundsLeadershipService>());

        return services;
    }

    public static IEndpointRouteBuilder MapRoundsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHub<RoundsHub>(RoundsHub.Path).RequireAuthorization();
        endpoints.MapGetSalaRounds();
        endpoints.MapGetRoundVerification();

        return endpoints;
    }
}
