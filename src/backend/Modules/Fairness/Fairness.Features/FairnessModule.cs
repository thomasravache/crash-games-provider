using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Fairness.Features;

/// <summary>Entry points the Host calls to compose the Fairness module (ADR-0001).</summary>
public static class FairnessModule
{
    public static IServiceCollection AddFairnessModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }

    public static IEndpointRouteBuilder MapFairnessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}
