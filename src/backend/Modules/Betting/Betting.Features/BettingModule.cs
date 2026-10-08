using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Features;

/// <summary>Entry points the Host calls to compose the Betting module (ADR-0001).</summary>
public static class BettingModule
{
    public static IServiceCollection AddBettingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }

    public static IEndpointRouteBuilder MapBettingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}
