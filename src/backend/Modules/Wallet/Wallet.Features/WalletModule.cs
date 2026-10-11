using Apogeu.Wallet.Features.Balances;
using Apogeu.Wallet.Features.Demo;
using Apogeu.Wallet.Infrastructure;
using Apogeu.Wallet.Infrastructure.Observability;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Apogeu.Wallet.Features;

/// <summary>Entry points the Host calls to compose the Wallet module (ADR-0001).</summary>
public static class WalletModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>
    /// Registers <c>IOperatorWallet</c> and <c>IDemoWallet</c> (demo adapter) on the PostgreSQL of
    /// <c>ConnectionStrings:apogeu</c>, read from the host configuration when first used, on a pool of the module's own bounded
    /// by <c>Wallet:MaxPoolSize</c>; the schema <c>wallet</c> is migrated at startup in Development only (ADR-0010).
    /// </summary>
    public static IServiceCollection AddWalletModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<WalletOptions>().BindConfiguration(WalletOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        // The module's own Npgsql pool is bounded (Wallet:MaxPoolSize), so no burst opens more connections than that (SPEC-0032).
        services.AddWalletInfrastructure(
            provider => provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."),
            provider => provider.GetRequiredService<IOptions<WalletOptions>>().Value.MaxPoolSize);
        services.AddHostedService<WalletDevelopmentMigrations>();

        // The module's meter and activity source reach the Host's OpenTelemetry pipeline (ADR-0017, SPEC-0021 §8).
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(WalletMetrics.MeterName));
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(WalletMetrics.MeterName));

        services.AddAuthorization();

        return services;
    }

    /// <summary>Maps the player REST API of the wallet (SPEC-0021 §6): <c>GET /api/v1/wallet/balance</c> and <c>POST /api/v1/wallet/demo/recharge</c>.</summary>
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // SPEC-0009 (ADR-0015): denied by default, so every endpoint of the module declares the player-session policy.
        endpoints.MapGetBalance();
        endpoints.MapRecharge();

        return endpoints;
    }
}
