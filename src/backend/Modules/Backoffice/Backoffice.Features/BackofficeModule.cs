using Apogeu.Backoffice.Features.Meta;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apogeu.Backoffice.Features;

/// <summary>Entry points the Host calls to compose the Backoffice module (ADR-0001).</summary>
public static class BackofficeModule
{
    public static IServiceCollection AddBackofficeModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    public static IEndpointRouteBuilder MapBackofficeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGetMeta();

        return endpoints;
    }
}
