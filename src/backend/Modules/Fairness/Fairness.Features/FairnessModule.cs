using Apogeu.Fairness.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Fairness.Features;

/// <summary>Entry points the Host calls to compose the Fairness module (ADR-0001).</summary>
public static class FairnessModule
{
    /// <summary>Name of the connection string of the module database (<c>ConnectionStrings:apogeu</c>).</summary>
    public const string ConnectionName = "apogeu";

    /// <summary>
    /// Registers <c>IFairnessService</c> on the PostgreSQL of <c>ConnectionStrings:apogeu</c> (read from the host
    /// configuration when first used) and applies the schema <c>fairness</c> migrations at startup in Development only;
    /// elsewhere the pipeline applies them (ADR-0010).
    /// </summary>
    public static IServiceCollection AddFairnessModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFairnessInfrastructure(provider =>
            provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'."));
        services.AddHostedService<FairnessDevelopmentMigrations>();

        return services;
    }

    public static IEndpointRouteBuilder MapFairnessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}
