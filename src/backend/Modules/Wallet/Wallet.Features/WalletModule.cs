using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Wallet.Features;

/// <summary>Entry points the Host calls to compose the Wallet module (ADR-0001).</summary>
public static class WalletModule
{
    public static IServiceCollection AddWalletModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }

    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}
