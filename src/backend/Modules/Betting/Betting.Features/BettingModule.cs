using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Features;

/// <summary>Entry points the Host calls to compose the Betting module (ADR-0001).</summary>
public static class BettingModule
{
    /// <summary>
    /// Registers the hub <c>/hubs/bets</c> and the <see cref="IRoundFeed"/> fed by the Rounds integration events (SPEC-0022);
    /// needs <c>IOperatorLimits</c> (Operators), <c>IOperatorWallet</c> (Wallet) and <see cref="TimeProvider"/> (Host).
    /// </summary>
    public static IServiceCollection AddBettingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSignalR();
        services.AddSingleton<IRoundFeed, RoundFeed>();

        return services;
    }

    public static IEndpointRouteBuilder MapBettingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // SPEC-0009 (ADR-0015): denied by default, so the hub declares the player-session policy.
        endpoints.MapHub<BetsHub>(BetsHub.Path).RequireAuthorization(SecurityPolicies.PlayerSession);

        return endpoints;
    }
}
