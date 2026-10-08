using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Operators.Features;

/// <summary>Entry points the Host calls to compose the Operators module (ADR-0001).</summary>
public static class OperatorsModule
{
    public static IServiceCollection AddOperatorsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }

    public static IEndpointRouteBuilder MapOperatorsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}
