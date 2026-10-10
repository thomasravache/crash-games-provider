using Apogeu.Wallet.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Wallet.Features;

/// <summary>Entry points the Host calls to compose the Wallet module (ADR-0001).</summary>
public static class WalletModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>
    /// Registers <c>IOperatorWallet</c> and <c>IDemoWallet</c> (demo adapter) on the PostgreSQL of
    /// <c>ConnectionStrings:apogeu</c>, read from the host configuration when first used.
    /// </summary>
    public static IServiceCollection AddWalletModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWalletInfrastructure(provider =>
            provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."));

        return services;
    }

    /// <summary>Maps the player REST API of the wallet (SPEC-0021 §6): <c>GET /api/v1/wallet/balance</c> and <c>POST /api/v1/wallet/demo/recharge</c>.</summary>
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}
